using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderElectionRun.KServices
{
	static class Ext
	{
		// Reference: https://stackoverflow.com/a/64236441/1510539
		public static IEnumerable<string> SplitArgs( this string commandLine )
		{
			StringBuilder ArgBuilder = new();

			bool quoted = false;
			bool escaped = false;
			bool started = false;
			bool allowcaret = false;
			for ( int i = 0; i < commandLine.Length; i++ )
			{
				char chr = commandLine[ i ];

				if ( chr == '^' && !quoted )
				{
					if ( allowcaret )
					{
						ArgBuilder.Append( chr );
						started = true;
						escaped = false;
						allowcaret = false;
					}
					else if ( i + 1 < commandLine.Length && commandLine[ i + 1 ] == '^' )
					{
						allowcaret = true;
					}
					else if ( i + 1 == commandLine.Length )
					{
						ArgBuilder.Append( chr );
						started = true;
						escaped = false;
					}
				}
				else if ( escaped )
				{
					ArgBuilder.Append( chr );
					started = true;
					escaped = false;
				}
				else if ( chr == '"' )
				{
					quoted = !quoted;
					started = true;
				}
				else if ( chr == '\\' && i + 1 < commandLine.Length && commandLine[ i + 1 ] == '"' )
				{
					escaped = true;
				}
				else if ( chr == ' ' && !quoted )
				{
					if ( started ) yield return ArgBuilder.ToString();
					ArgBuilder.Clear();
					started = false;
				}
				else
				{
					ArgBuilder.Append( chr );
					started = true;
				}
			}

			if ( started ) yield return ArgBuilder.ToString();
		}

		public static IEnumerable<T> Rests<T>( this IEnumerator<T> Enumerator )
		{
			while ( Enumerator.MoveNext() )
			{
				yield return Enumerator.Current;
			}
		}

		public static T First<T>( this IEnumerator<T> Enumerator )
		{
			Enumerator.MoveNext();
			return Enumerator.Current;
		}

	}
}
