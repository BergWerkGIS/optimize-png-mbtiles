using System;
using System.Collections.Generic;
using System.Text;

namespace optimizepngmbtiles {
	public class CruncherSettings {

		public CruncherSettings() {
			//Threads = (int)Math.Floor(((double)Environment.ProcessorCount) * 1.5d);
			Threads = Environment.ProcessorCount;
		}

		public string MbTilesPath { get; set; }

		public int MinQuality { get; set; } = 0;
		public int MaxQuality { get; set; } = 20;
		public int Speed { get; set; } = 1;
		public int BatchSize { get; set; } = 1000;
		public int Threads { get; set; }


		public override string ToString() {
			StringBuilder sb = new StringBuilder();

			sb.AppendLine($"MbTiles         : {MbTilesPath}");
			sb.AppendLine($"Threads         : {Threads}");
			sb.AppendLine($"Batch size      : {BatchSize}");
			sb.AppendLine($"PNG min quality : {MinQuality}");
			sb.AppendLine($"PNG max quality : {MaxQuality}");
			sb.AppendLine($"PNG speed       : {Speed}");

			return sb.ToString();
		}




	}
}
