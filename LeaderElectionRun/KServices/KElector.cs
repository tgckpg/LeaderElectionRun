﻿using k8s.Autorest;
using k8s.LeaderElection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LeaderElectionRun.KServices
{
	// Extracted from https://github.com/kubernetes-client/csharp/blob/4db390f3afbf93256b1ed954db1db20f2b2b1839/src/KubernetesClient/LeaderElection/LeaderElector.cs
	// Applied patches for issue #841, #842
	public class KElector : IDisposable
	{
		private const double JitterFactor = 1; // Issue #842
		private readonly LeaderElectionConfig config;
		public event Action OnStartedLeading;
		public event Action OnStoppedLeading;
		public event Action<string> OnNewLeader;
		private volatile LeaderElectionRecord observedRecord;
		private DateTimeOffset observedTime = DateTimeOffset.MinValue;
		private string reportedLeader;

		private readonly ILogger Logger;

		public KElector( LeaderElectionConfig config )
		{
			Logger = KLog.GetLogger<KElector>();
			this.config = config;
		}

		public bool IsLeader()
		{
			return observedRecord?.HolderIdentity != null && observedRecord?.HolderIdentity == config.Lock.Identity;
		}

		public string GetLeader()
		{
			return observedRecord?.HolderIdentity;
		}

		public async Task RunAsync( CancellationToken cancellationToken = default )
		{
			await AcquireAsync( cancellationToken ).ConfigureAwait( false );

			try
			{
				OnStartedLeading?.Invoke();

				// renew loop
				for (; ; )
				{
					cancellationToken.ThrowIfCancellationRequested();
					var acq = Task.Run( async () =>
					{
						try
						{
							while ( !await TryAcquireOrRenew( cancellationToken ).ConfigureAwait( false ) )
							{
								await Task.Delay( config.RetryPeriod, cancellationToken ).ConfigureAwait( false );
								MaybeReportTransition();
							}
						}
						catch
						{
							// ignore
							return false;
						}

						return true;
					} );


					if ( await Task.WhenAny( acq, Task.Delay( config.RenewDeadline, cancellationToken ) )
						.ConfigureAwait( false ) == acq )
					{
						var succ = await acq.ConfigureAwait( false );

						if ( succ )
						{
							await Task.Delay( config.RetryPeriod, cancellationToken ).ConfigureAwait( false );
							// retry
							continue;
						}

						// renew failed
					}

					// timeout
					break;
				}
			}
			finally
			{
				OnStoppedLeading?.Invoke();
			}
		}

		private async Task<bool> TryAcquireOrRenew( CancellationToken cancellationToken )
		{
			var l = config.Lock;
			var leaderElectionRecord = new LeaderElectionRecord()
			{
				HolderIdentity = l.Identity,
				LeaseDurationSeconds = ( int ) config.LeaseDuration.TotalSeconds,
				AcquireTime = DateTime.UtcNow,
				RenewTime = DateTime.UtcNow,
				LeaderTransitions = 0,
			};

			// 1. obtain or create the ElectionRecord

			LeaderElectionRecord oldLeaderElectionRecord = null;
			try
			{
				oldLeaderElectionRecord = await l.GetAsync( cancellationToken ).ConfigureAwait( false );
			}
			catch ( HttpOperationException e )
			{
				Logger.LogWarning( e, $"ERR {e.Response.StatusCode}" );
				if ( e.Response.StatusCode != HttpStatusCode.NotFound )
				{
					return false;
				}
			}

			if ( oldLeaderElectionRecord?.AcquireTime == null ||
				oldLeaderElectionRecord?.RenewTime == null ||
				oldLeaderElectionRecord?.HolderIdentity == null )
			{
				var created = await l.CreateAsync( leaderElectionRecord, cancellationToken ).ConfigureAwait( false );
				if ( created )
				{
					observedRecord = leaderElectionRecord;
					observedTime = DateTimeOffset.Now;
					return true;
				}

				return false;
			}

			// 2. Record obtained, check the Identity & Time
			if ( IsModified( observedRecord, oldLeaderElectionRecord ) ) // Issue #841
			{
				observedRecord = oldLeaderElectionRecord;
				observedTime = DateTimeOffset.Now;
			}

			if ( !string.IsNullOrEmpty( oldLeaderElectionRecord.HolderIdentity )
				&& observedTime + config.LeaseDuration > DateTimeOffset.Now
				&& !IsLeader() )
			{
				// lock is held by %v and has not yet expired", oldLeaderElectionRecord.HolderIdentity
				return false;
			}

			// 3. We're going to try to update. The leaderElectionRecord is set to it's default
			// here. Let's correct it before updating.
			if ( IsLeader() )
			{
				leaderElectionRecord.AcquireTime = oldLeaderElectionRecord.AcquireTime;
				leaderElectionRecord.LeaderTransitions = oldLeaderElectionRecord.LeaderTransitions;
			}
			else
			{
				leaderElectionRecord.LeaderTransitions = oldLeaderElectionRecord.LeaderTransitions + 1;
			}

			var updated = await l.UpdateAsync( leaderElectionRecord, cancellationToken ).ConfigureAwait( false );
			if ( !updated )
			{
				return false;
			}

			observedRecord = leaderElectionRecord;
			observedTime = DateTimeOffset.Now;

			return true;
		}

		private bool IsModified( LeaderElectionRecord observedRecord, LeaderElectionRecord oldLeaderElectionRecord )
		{
			if ( observedRecord == null )
				return true;

			return !(
				observedRecord.AcquireTime == oldLeaderElectionRecord.AcquireTime
				&& observedRecord.RenewTime == oldLeaderElectionRecord.RenewTime
				&& observedRecord.HolderIdentity == oldLeaderElectionRecord.HolderIdentity
			);
		}

		private async Task AcquireAsync( CancellationToken cancellationToken )
		{
			var delay = ( int ) config.RetryPeriod.TotalMilliseconds;
			for (; ; )
			{
				try
				{
					var acq = TryAcquireOrRenew( cancellationToken );

					if ( await Task.WhenAny( acq, Task.Delay( delay, cancellationToken ) )
						.ConfigureAwait( false ) == acq )
					{
						if ( await acq.ConfigureAwait( false ) )
						{
							return;
						}

						// wait RetryPeriod since acq return immediately
						await Task.Delay( delay, cancellationToken ).ConfigureAwait( false );
					}

					// else timeout

					delay = ( int ) ( delay * JitterFactor );
				}
				finally
				{
					MaybeReportTransition();
				}
			}
		}

		private void MaybeReportTransition()
		{
			if ( observedRecord == null )
			{
				return;
			}

			if ( observedRecord.HolderIdentity == reportedLeader )
			{
				return;
			}

			reportedLeader = observedRecord.HolderIdentity;

			OnNewLeader?.Invoke( reportedLeader );
		}

		protected virtual void Dispose( bool disposing )
		{
			if ( disposing )
			{
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}
	}
}