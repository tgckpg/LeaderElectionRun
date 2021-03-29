using LeaderElectionRun.KServices;
using System;
using CommandLine;

namespace LeaderElectionRun
{
	class Program
	{
		public class Options
		{
			[Option( 'p', "pid-file", Required = false, HelpText = "Start/Stop leading by monitoring the pid file, will not stop if unspecified" )]
			public string PIdFile { get; set; }

			[Option( 'n', "namespace", Required = false, HelpText = "The namespace in a k8s cluster" )]
			public string Namespace { get; set; } = "default";

			[Option( 'm', "shared-lock", Required = true, HelpText = "The name of the shared lock" )]
			public string LockName { get; set; }

			[Option( 'i', "identity", Required = false, HelpText = "The leader identity, defaults to Environment::HOSTNAME" )]
			public string Identity { get; set; } = Environment.GetEnvironmentVariable( "HOSTNAME" );

			[Option( 'e', "elect", Required = false, HelpText = "Process to run when a new leader is elected" )]
			public string OnElectExec { get; set; }

			[Option( 's', "start", Required = false, HelpText = "Process to run when started leading" )]
			public string OnStartExec { get; set; }

			[Option( 'x', "stop", Required = false, HelpText = "Process to run when stopped leading" )]
			public string OnStopExec { get; set; }

			[Option( 't', "test", Required = false, HelpText = "Test run commands, in the order of -e -s -x." )]
			public bool Test { get; set; }
		}

		static void Main( string[] args )
		{
			Parser.Default.ParseArguments<Options>( args )
				.WithParsed( Start )
				.WithNotParsed( x => Environment.Exit( ( x.IsVersion() || x.IsHelp() ) ? 0 : 1 ) );
		}

		private static void Start( Options Opt )
		{
			if ( string.IsNullOrEmpty( Opt.Identity ) )
			{
				Console.WriteLine( "Identity cannot be empty. Use -i or set a HOSTNAME environment variable." );
				Environment.Exit( 1 );
			}

			KStart Instance = new( Opt.Namespace, Opt.LockName, Opt.Identity )
			{
				ExecElect = Opt.OnElectExec,
				ExecStart = Opt.OnStartExec,
				ExecStop = Opt.OnStopExec
			};

			if( Opt.Test )
			{
				Console.WriteLine( "Test mode" );
				Instance.Test( Instance.ExecElect )?.WaitForExit();
				Instance.Test( Instance.ExecStart )?.WaitForExit();
				Instance.Test( Instance.ExecStop )?.WaitForExit();
				Environment.Exit( 0 );
			}

			if ( string.IsNullOrEmpty( Opt.PIdFile ) )
			{
				Instance.Start();
			}
			else
			{
				Instance.Start( Opt.PIdFile );
			}
		}

	}
}
