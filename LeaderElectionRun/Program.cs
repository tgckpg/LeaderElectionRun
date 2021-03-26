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

            [Option( 'x', "stop", Required = false, HelpText = "Process to run when stopped leading" )]
            public string OnStopExec { get; set; }

            [Option( 's', "start", Required = false, HelpText = "Process to run when started leading" )]
            public string OnStartExec { get; set; }
        }

        static void Main( string[] args )
        {
            Parser.Default.ParseArguments<Options>( args ).WithParsed( Start );
        }

        private static void Start( Options Opt )
        {
            if ( string.IsNullOrEmpty( Opt.Identity ) )
            {
                Console.WriteLine( "Identity cannot be empty. Use -i or set a HOSTNAME environment variable." );
                Environment.Exit( 1 );
            }

            KStart Instance = new KStart( Opt.Namespace, Opt.LockName, Opt.Identity );
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
