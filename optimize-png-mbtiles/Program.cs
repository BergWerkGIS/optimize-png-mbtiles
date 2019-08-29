using System;
using System.IO;
using System.Collections.Generic;
using Mono.Options;


namespace optimizepngmbtiles {


	public class TileData {
		public int z;
		public long x;
		public long y;
	}


	public class Program {


		static int Main(string[] args) {


			CruncherSettings cs = new CruncherSettings();

			var options = new OptionSet {
				{"f|mbtiles=", $"Path to MBTiles", (string mb)=> cs.MbTilesPath=mb },
				{"m|min-quality=", $"PNG min quality [0..100]. Default:{cs.MinQuality}", (int mq)=> cs.MinQuality=mq },
				{"x|max-quality=", $"PNG max quality [0..100]. Default:{cs.MaxQuality}", (int mq)=> cs.MaxQuality=mq },
				{"s|speed=", $"PNG speed [1..10]. Slower better quality. Default:{cs.Speed}", (int s)=> cs.Speed=s },
				{"t|threads=", $"Threads. Default (Processors):{cs.Threads}", (int t)=> cs.Threads=t },
				{"b|batch-size=", $"Batch size. Number of tiles processed in one batch. Default:{cs.BatchSize}", (int bs)=>cs.BatchSize=bs }
			};

			options.WriteOptionDescriptions(Console.Out);
			Console.WriteLine();

			List<string> extra = new List<string>();
			try {
				extra = options.Parse(args);
			}
			catch (OptionException oe) {
				Console.WriteLine("Error parsing parameters:");
				Console.WriteLine(oe.Message);
				return 1;
			}

			if (extra.Count > 0) {
				Console.WriteLine("Unknown parameters:");
				Console.WriteLine(string.Join(Environment.NewLine, extra));
				return 1;
			}

			if (string.IsNullOrWhiteSpace(cs.MbTilesPath)) {
				Console.WriteLine("Missing parameter: MBTiles file not specified");
				return 1;
			}

			if (!File.Exists(cs.MbTilesPath)) {
				Console.WriteLine($"MBTiles file does not exist: {cs.MbTilesPath}");
				return 1;
			}


			Console.WriteLine("Using parameters:");
			Console.WriteLine(cs.ToString());
			Console.WriteLine();

			Cruncher cruncher = new Cruncher(cs);
			cruncher.Go();


			return 0;
		}





	}
}
