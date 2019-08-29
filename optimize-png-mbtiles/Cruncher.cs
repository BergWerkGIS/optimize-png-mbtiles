using ImageQuant;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using static System.FormattableString;


namespace optimizepngmbtiles {


	public class Cruncher {


		private static CruncherSettings _cs;
		private static DateTime _dtStart;
		private static long _cnter = 0;
		private static SQLiteConnection _conn;


		public Cruncher(CruncherSettings cs) {
			_cs = cs;
		}


		public void Go() {

			_dtStart = DateTime.Now;

			try {

				ThreadPool.SetMinThreads(_cs.Threads, _cs.Threads);
				ThreadPool.SetMaxThreads(_cs.Threads, _cs.Threads);

				string sqliteFile = Path.GetFullPath(_cs.MbTilesPath);
				double sizeStart = getFileSize(sqliteFile);
				Console.WriteLine($"sqlite: {sqliteFile}");
				Console.WriteLine(Invariant($"size: {sizeStart:0.00}GB"));
				string connStr = $"Data Source={sqliteFile};";
				_conn = new SQLiteConnection(connStr);
				_conn.Open();

				executeCmd("PRAGMA synchronous=OFF");
				executeCmd("PRAGMA count_changes=OFF");
				executeCmd("PRAGMA journal_mode=MEMORY");
				executeCmd("PRAGMA temp_store=MEMORY");

				List<List<TileData>> batches = new List<List<TileData>>();

				using (SQLiteCommand cmd = _conn.CreateCommand()) {
					cmd.CommandType = CommandType.Text;
					//cmd.CommandText = "SELECT zoom_level, tile_column, tile_row, tile_data FROM tiles where zoom_level";
					cmd.CommandText = "SELECT zoom_level, tile_column, tile_row FROM tiles where zoom_level";
					using (SQLiteDataReader reader = cmd.ExecuteReader()) {
						if (!reader.HasRows) {
							Console.WriteLine("MBTiles file does not contain any tiles");
							return;
						}
						List<TileData> workItem = new List<TileData>(_cs.BatchSize);
						while (reader.Read()) {
							int z = Convert.ToInt32(reader[0]);
							long x = Convert.ToInt64(reader[1]);
							long y = Convert.ToInt64(reader[2]);

							TileData td = new TileData {
								z = z,
								x = x,
								y = y,
							};

							workItem.Add(td);

							/////////////////////
							///////////////////////
							///////////////
							/// TODO: last batch might get lost
							/////////////////////
							/////////////////////
							/////////////////////
							/////////////////////

							if (workItem.Count == _cs.BatchSize) {
								batches.Add(workItem);
								workItem = new List<TileData>(_cs.BatchSize);
							}
						}
					}
				}


				foreach (List<TileData> workItem in batches) {
					ThreadPool.QueueUserWorkItem(crunchTile, new List<TileData>(workItem));
				}


				while (ThreadPool.PendingWorkItemCount > 0) {
					Thread.Sleep(1000);
				}


				Console.WriteLine($"cnter:{_cnter} CompletedWorkItemCount:{ThreadPool.CompletedWorkItemCount}");


				DateTime dtVacStart = DateTime.Now;
				try {
					Console.WriteLine("VACUUM ....  ");
					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = "VACUUM;";
						cmd.ExecuteNonQuery();
					}
				}
				catch (Exception ex) {
					Console.WriteLine("Error compressing database:");
					Console.WriteLine(ex.ToString());
				}

				DateTime dtStop = DateTime.Now;
				TimeSpan spanOverall = dtStop.Subtract(_dtStart);
				TimeSpan spanVac = dtStop.Subtract(dtVacStart);

				double sizeDone = getFileSize(sqliteFile);

				Console.WriteLine("");
				Console.WriteLine(Invariant($"SIZE    : {sizeStart:0.00}GB => {sizeDone:0.00}GB"));
				Console.WriteLine(Invariant($"VACCUMM : {spanVac.Hours}h {spanVac.Minutes}m {spanVac.Seconds}s ({dtVacStart:yyyyMMdd HHmmss} => {dtStop:yyyyMMdd HHmmss})"));
				Console.WriteLine(Invariant($"OVERALL : {spanOverall.Hours}h {spanOverall.Minutes}m {spanOverall.Seconds}s ({_dtStart:yyyyMMdd HHmmss} => {dtStop:yyyyMMdd HHmmss})"));
			}
			catch (Exception ex) {
				Console.WriteLine("ERROR:");
				Console.WriteLine(ex.ToString());
			}
			finally {
				if (null != _conn) {
					_conn.Close();
					_conn.Dispose();
					_conn = null;
				}
			}
		}




		private static void crunchTile(object paramData) {

			Thread.CurrentThread.Priority = ThreadPriority.Lowest;

			List<TileData> tiles = paramData as List<TileData>;
			if (null == tiles) { return; }

			foreach (TileData tile in tiles) {

				// debug message in case of error
				string debugQuery = $"SELECT * FROM TILES WHERE zoom_level={tile.z} AND tile_column={tile.x} and tile_row={tile.y}";

				try {
					byte[] data = null;
					using (SQLiteCommand cmd = _conn.CreateCommand()) {
						cmd.CommandType = CommandType.Text;
						cmd.CommandText = $"SELECT tile_data FROM tiles WHERE zoom_level={tile.z} AND tile_column={tile.x} and tile_row={tile.y}";
						using (SQLiteDataReader reader = cmd.ExecuteReader()) {
							if (!reader.HasRows) { Console.WriteLine("no data"); return; }
							if (!reader.Read()) { return; }
							data = (byte[])reader[0];
						}
					}


					//optimize
					byte[] optimized = ImageQuantWrapper.Process(data, _cs.MinQuality, _cs.MaxQuality, _cs.Speed);
					if (null == optimized) {
						Console.WriteLine($"optimization failed, TMS: {debugQuery}");
						return;
					}

					// write 
					using (SQLiteCommand cmdUpdate = _conn.CreateCommand()) {
						cmdUpdate.CommandType = CommandType.Text;
						cmdUpdate.CommandText = $"UPDATE tiles SET tile_data=@img WHERE zoom_level={tile.z} AND tile_column={tile.x} and tile_row={tile.y}";
						cmdUpdate.Parameters.AddWithValue("@img", optimized);
						int rowsAffected = cmdUpdate.ExecuteNonQuery();
						if (1 != rowsAffected) {
							Console.WriteLine($"rowsAffected != 1: {debugQuery}");
							return;
						}
					}

					long cnter = Interlocked.Increment(ref _cnter);

					if (0 == cnter % 5000) {
						DateTime dtCurrent = DateTime.Now;
						TimeSpan elapsed = dtCurrent.Subtract(_dtStart);
						double tps = ((double)cnter) / elapsed.TotalSeconds;
						//Console.WriteLine($"{Environment.NewLine}{cnter} {_dtStart:yyyyMMdd HHmmss} => {dtCurrent:yyyyMMdd HHmmss} {elapsed.Hours}h {elapsed.Minutes}m {elapsed.Seconds}s");
						Console.WriteLine(Invariant($"{cnter,6} {elapsed.Hours,2}h {elapsed.Minutes,2}m {elapsed.Seconds,2}s {tps,7:0.0} tiles/second   batches left:{ThreadPool.PendingWorkItemCount}"));
					}
				}
				catch (Exception ex) {
					Console.WriteLine($"ERROR processing tile {tile}:{Environment.NewLine}{ex.ToString()}");
				}
			}
		}


		private double getFileSize(string fileName) {
			FileInfo fi = new FileInfo(fileName);
			return ((double)fi.Length / 1024.0d / 1024.0d / 1024.0d);
		}


		public static bool executeCmd(string cmdSql) {

			try {

				using (SQLiteCommand cmd = _conn.CreateCommand()) {

					cmd.CommandType = CommandType.Text;
					cmd.CommandText = cmdSql.ToString();

					int rowsAffected = cmd.ExecuteNonQuery();
				}

				return true;
			}
			catch (Exception ex) {
				Console.WriteLine($"ERROR executing database command:{Environment.NewLine}{cmdSql}{Environment.NewLine}{ex}");
				return false;
			}
		}








	}
}
