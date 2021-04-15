using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderElectionRun.KServices
{
	class KLog
	{
		private static KLog _Inst;
		public static KLog Instance => _Inst ??= new KLog();

		private readonly LoggerFactory _LogFactory;

		public KLog()
		{
			_LogFactory = new LoggerFactory();
			_LogFactory.AddProvider( new KLogProvider() );
		}

		public static ILogger GetLogger<T>()
			=> Instance._LogFactory.CreateLogger<T>();
	}

	class KLogger : ILogger
	{
		public Dictionary<LogLevel, string> LogLevelMap = new()
		{
			{ LogLevel.Information, "INFO" },
			{ LogLevel.Error, "ERROR" },
			{ LogLevel.Warning, "WARN" },
		};

		public string Cat { get; set; }

		public IDisposable BeginScope<TState>( TState state ) => default;

		public bool IsEnabled( LogLevel logLevel )
			=> true;

		public void Log<TState>( LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter )
		{
			Console.WriteLine( $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{Cat}][{LogLevelMap[ logLevel ]}] {formatter( state, exception )}" );
			if( exception != null )
			{
				Console.WriteLine( exception );
			}
		}
	}

	class KLogProvider : ILoggerProvider
	{
		private readonly ConcurrentDictionary<string, KLogger> _Loggers = new();

		public ILogger CreateLogger( string Cat )
			=> _Loggers.GetOrAdd( Cat, new KLogger() { Cat = Cat.Split( '.' ).Last() } );

		public void Dispose()
			=> _Loggers.Clear();
	}
}
