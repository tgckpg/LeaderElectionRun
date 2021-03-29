﻿using FormatWith;
using k8s;
using k8s.LeaderElection;
using k8s.LeaderElection.ResourceLock;
using LeaderElectionRun.KServices.ABIObjects;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LeaderElectionRun.KServices
{
	class KStart
	{
		public readonly string Namespace;
		public readonly string Id;
		public readonly string LockName;

		public string ExecStart { get; set; }
		public string ExecStop { get; set; }
		public string ExecElect { get; set; }

		private readonly LeaderElector Elector;
		private readonly ILogger Logger;

		public KStart( string Namespace, string LockName, string Id )
		{
			Logger = KLog.GetLogger<KStart>();

			this.Namespace = Namespace;
			this.Id = Id;
			this.LockName = LockName;

			Kubernetes Kube = new( KubernetesClientConfiguration.BuildDefaultConfig() );
			EndpointsLock EndPointLock = new( Kube, Namespace, LockName, Id );
			Elector = new( new LeaderElectionConfig( EndPointLock )
			{
				LeaseDuration = TimeSpan.FromSeconds( 5 ),
				RetryPeriod = TimeSpan.FromSeconds( 5 )
			} );

			Elector.OnNewLeader += Elector_OnNewLeader;
			Elector.OnStartedLeading += Elector_OnStartedLeading;
			Elector.OnStoppedLeading += Elector_OnStoppedLeading;
		}

		public void Start()
		{
			while ( true )
			{
				Logger.LogInformation( $"Started: {Id}" );

				using ( CancellationTokenSource TokenSource = new() )
				{
					_ = Elector.RunAsync( TokenSource.Token );
					Console.ReadKey( true );
					TokenSource.Cancel();
				}

				Logger.LogInformation( $"Stopped {Id}" );
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
				catch ( AccessViolationException )
				{
					Logger.LogError( $"Access denied while reading: {PIdFile}" );
					Thread.Sleep( 2000 );
					continue;
				}
				catch ( FileNotFoundException )
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

				Logger.LogInformation( $"Started. Id: {Id}, Monitor: {P.ProcessName}, PID: {PId}" );
				using ( CancellationTokenSource TokenSource = new() )
				{
					_ = Elector.RunAsync( TokenSource.Token );
					P.WaitForExit();
					TokenSource.Cancel();
				}

				Logger.LogInformation( $"Stopped. Process exited." );
			}
		}

		public Process Test( string ExecPath )
		{
			ExecEventArgs e = new();
			return Exec( ExecPath, e );
		}

		private void Elector_OnStartedLeading()
		{
			Logger.LogInformation( $"Started Leading: {Id}" );
			Exec( ExecStart, new ExecEventArgs() { Id = Id } );
		}

		private void Elector_OnStoppedLeading()
		{
			Logger.LogInformation( $"Stopped Leading: {Id}" );
			Exec( ExecStop, new ExecEventArgs() { Id = Id } );
		}

		private void Elector_OnNewLeader( string LeaderId )
		{
			Logger.LogInformation( $"Elected Leader: {LeaderId}" );
			Exec( ExecElect, new ExecEventArgs() { Id = Id, LeaderId = LeaderId } );
		}

		private Process Exec( string ExecPath, ExecEventArgs e )
		{
			if ( string.IsNullOrEmpty( ExecPath ) )
				return null;

			try
			{
				ExecPath = ExecPath.FormatWith( e );
			}
			catch( Exception Ex )
			{
				Logger.LogError( Ex, $"Failed to format command: {ExecPath}" );
				return null;
			}

			try
			{
				Logger.LogInformation( $"Exec: {ExecPath}" );
				IEnumerator<string> Args = ExecPath.SplitArgs().GetEnumerator();
				ProcessStartInfo PStart = new ProcessStartInfo( Args.First() );
				foreach ( string s in Args.Rests() )
					PStart.ArgumentList.Add( s );

				PStart.CreateNoWindow = true;
				PStart.UseShellExecute = false;
				PStart.RedirectStandardOutput = true;
				PStart.RedirectStandardError = true;

				Process P = new Process() { StartInfo = PStart };

				P.EnableRaisingEvents = true;
				P.Exited += ( object sender, EventArgs e ) =>
				{
					Process P = ( Process ) sender;
					if ( P.ExitCode != 0 )
					{
						Logger.LogError( $"Command exit with code {P.ExitCode}: {ExecPath}" );
					}
				};

				P.OutputDataReceived += P_OutputDataReceived;
				P.ErrorDataReceived += P_ErrorDataReceived;
				P.Start();
				P.BeginErrorReadLine();
				P.BeginOutputReadLine();
				return P;
			}
			catch ( Exception Ex )
			{
				Logger.LogError( Ex, $"Failed to exec: {ExecPath}" );
			}

			return null;
		}

		private void P_ErrorDataReceived( object sender, DataReceivedEventArgs e )
		{
			if ( e.Data == null )
				return;
			KLog.GetLogger<Process>().LogError( e.Data );
		}

		private void P_OutputDataReceived( object sender, DataReceivedEventArgs e )
		{
			if ( e.Data == null )
				return;
			KLog.GetLogger<Process>().LogInformation( e.Data );
		}
	}
}
