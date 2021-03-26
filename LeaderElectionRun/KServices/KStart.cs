using k8s;
using k8s.LeaderElection;
using k8s.LeaderElection.ResourceLock;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LeaderElectionRun.KServices
{
    class KStart
    {
		public readonly string Namespace;
        public string Identity;
        public string LockName;

		private CancellationTokenSource CTS;
		private readonly LeaderElector Elector;

		private readonly ILogger Logger;

		public KStart( string Namespace, string LockName, string Identity )
		{
			Logger = KLog.GetLogger<KStart>();

			this.Namespace = Namespace;
			this.Identity = Identity;
			this.LockName = LockName;

			Kubernetes Kube = new( KubernetesClientConfiguration.BuildDefaultConfig() );
			EndpointsLock EndPointLock = new( Kube, Namespace, LockName, Identity );
			Elector = new( new LeaderElectionConfig( EndPointLock )
			{
				LeaseDuration = TimeSpan.FromSeconds( 1 ),
				RetryPeriod = TimeSpan.FromMilliseconds( 400 )
			} );

			Elector.OnNewLeader += Elector_OnNewLeader;
			Elector.OnStartedLeading += Elector_OnStartedLeading;
			Elector.OnStoppedLeading += Elector_OnStoppedLeading;
		}

		public void Start()
		{
			while ( true )
			{
				CTS = new();
				_ = Elector.RunAsync( CTS.Token );
				Logger.LogInformation( "Started" );
				Console.ReadKey( true );
				CTS.Cancel();
				CTS.Dispose();
				Logger.LogInformation( "Stopped" );
				Console.ReadKey( true );
			}
		}

		public void Start( string PIdFile )
		{
			while ( true )
			{
				int PId;
				try
				{
					using FileStream S = File.OpenRead( PIdFile );
					using StreamReader S2 = new( S );
					PId = int.Parse( S2.ReadLine() );
				}
				catch( AccessViolationException )
				{
					Logger.LogError( $"Access denied while reading: {PIdFile}" );
					Thread.Sleep( 2000 );
					continue;
				}
				catch( FileNotFoundException )
				{
					Logger.LogWarning( $"Waiting for file: {PIdFile}" );
					Thread.Sleep( 2000 );
					continue;
				}

				Process P;
				try
				{
					P = Process.GetProcessById( PId );
				}
				catch ( ArgumentException )
				{
					Logger.LogWarning( $"No such process({PId})" );
					Thread.Sleep( 2000 );
					continue;
				}

				Logger.LogInformation( $"Monitoring: {P.ProcessName}, PID: {PId}" );
				CTS = new();
				_ = Elector.RunAsync( CTS.Token );
				P.WaitForExit();
				CTS.Cancel();
				CTS.Dispose();
				Logger.LogInformation( "Process has exited" );
			}
		}

		private void Elector_OnStartedLeading()
		{
			Logger.LogInformation( $"Started Leading: {Identity}" );
		}

		private void Elector_OnStoppedLeading()
		{
			Logger.LogInformation( $"Stopped Leading: {Identity}" );
		}

		private void Elector_OnNewLeader( string obj )
		{
			Logger.LogInformation( $"New Leader: {Identity}" );
		}
	}
}
