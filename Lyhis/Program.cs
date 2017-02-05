using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace Lyhis {
    class Program {
        const int MODE_INVALID          = 0;
        const int MODE_RATE             = 1;
        const int MODE_FIX              = 2;
        const int MODE_STATS            = 3;
        const int MODE_IMPORT_RATING    = 4;

        static void Main(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("Lyhis.exe [command] [target directory] [rating]");
                Console.WriteLine("--");
                Console.WriteLine("Examples:");
                Console.WriteLine("Lyhis.exe rate \"C:\\presets\" 3");
                Console.WriteLine("Lyhis.exe fix \"C:\\presets\"");
                Console.WriteLine("Lyhis.exe stats \"C:\\presets\"");
                Console.WriteLine("Lyhis.exe import_rating \"C:\\presets_to\" \"C:\\presets_from\"");
                return;
            }

            int mode = MODE_INVALID;
            if (String.Compare(args[0], "rate", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_RATE;
            }
            else if (String.Compare(args[0], "fix", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_FIX;
            }
            else if (String.Compare(args[0], "stats", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_STATS;
            }
            else if (String.Compare(args[0], "import_rating", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_IMPORT_RATING;
            }

            if (mode == MODE_INVALID) {
                Console.WriteLine("No such command! Possible choices: rate, fix.");
                return;
            }

            string directory = args[1];
            if (!Directory.Exists(directory)) {
                Console.WriteLine("No such directory: " + directory);
                return;
            }

            string directory2 = "";
            if (mode == MODE_IMPORT_RATING) {
                directory2 = args[2];
                if (!Directory.Exists(directory2)) {
                    Console.WriteLine("No such directory: " + directory2);
                    return;
                }
            }

            double rating = 0;
            if (mode == MODE_RATE) {
                if (args.Length < 3) {
                    Console.WriteLine("No rating provided?");
                    return;
                }

                bool ok = Double.TryParse(args[2], out rating);
                if (!ok || rating < 0 || rating > 5) {
                    Console.WriteLine("Rating must be between 0 and 5.");
                    return;
                }
            }

            var ratings = new Dictionary<int, int>();

            var files = Directory.GetFiles(directory);

            var files2 = new Dictionary<string, string>();
            if (mode == MODE_IMPORT_RATING) {
                var fs = Directory.GetFiles(directory2);
                for (int i = 0; i < fs.Length; i++) {
                    var file = fs[i];
                    var fileName = Path.GetFileName(file);
                    files2[fileName] = file;
                }
            }

            Console.Write("Processing " + files.Length + " files ... ");

            double lastProgress = 0.0;

            for (int i = 0; i < files.Length; i++) {
                var file = files[i];

                StreamReader sr;
                string line;

                if (file.EndsWith(".milk", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".mil", StringComparison.InvariantCultureIgnoreCase)) {
                    int fileRating = -1;
                    if (mode == MODE_IMPORT_RATING) {
                        var fileName = Path.GetFileName(file);
                        if (files2.ContainsKey(fileName)) {
                            var file2 = files2[fileName];
                            sr = new StreamReader(file2, Encoding.ASCII);
                            line = null;
                            while ((line = sr.ReadLine()) != null) {
                                if (line.StartsWith("fRating=", StringComparison.InvariantCultureIgnoreCase) && line.Length >= 9) {
                                    fileRating = Convert.ToInt32(line.Substring(8, 1));
                                    break;
                                }
                            }
                            sr.Close();
                        }
                        else {
                            goto print_progress;
                        }
                    }

                    StringBuilder sb = new StringBuilder();
                    sr = new StreamReader(file, Encoding.ASCII);
                    line = null;
                    bool needsRewrite = false;
                    bool seenPresetHeader = false;
                    bool seenRating = false;
                    while ((line = sr.ReadLine()) != null) {
                        if (mode == MODE_RATE) {
                            if (line.StartsWith("fRating=", StringComparison.InvariantCultureIgnoreCase)) {
                                sb.AppendLine("fRating=" + rating.ToString("0.000", CultureInfo.InvariantCulture));
                                needsRewrite = true;
                            }
                            else {
                                sb.AppendLine(line);
                            }
                        }
                        else if (mode == MODE_FIX) {
                            if (line.StartsWith("[preset00]", StringComparison.InvariantCultureIgnoreCase)) {
                                if (seenPresetHeader) continue;
                                seenPresetHeader = true;
                            }
                            else if (line.StartsWith("fRating=", StringComparison.InvariantCultureIgnoreCase)) {
                                if (seenRating) continue;
                                seenRating = true;
                            }

                            sb.AppendLine(line);
                        }
                        else if (mode == MODE_STATS) {
                            if (line.StartsWith("fRating=", StringComparison.InvariantCultureIgnoreCase) && line.Length >= 9) {
                                var r = Convert.ToInt32(line.Substring(8, 1));
                                if (!ratings.ContainsKey(r)) {
                                    ratings[r] = 1;
                                }
                                else {
                                    ratings[r] = ratings[r] + 1;
                                }
                                break;
                            }
                        }
                        else if (mode == MODE_IMPORT_RATING) {
                            if (line.StartsWith("fRating=", StringComparison.InvariantCultureIgnoreCase)) {
                                var r = -1;
                                if (line.Length >= 9) {
                                    r = Convert.ToInt32(line.Substring(8, 1));
                                }
                                if (fileRating != -1 && fileRating != r) {
                                    sb.AppendLine("fRating=" + fileRating.ToString("0.000", CultureInfo.InvariantCulture));
                                    needsRewrite = true;
                                }
                            }
                            else {
                                sb.AppendLine(line);
                            }
                        }
                    }
                    sr.Close();

                    if (mode == MODE_FIX) needsRewrite = true;

                    if (needsRewrite) {
                        StreamWriter sw = new StreamWriter(file, false, Encoding.ASCII);
                        sw.Write(sb.ToString());
                        sw.Flush();
                        sw.Close();
                    }
                }
            print_progress:

                int progress = (int)Math.Round(Convert.ToDouble(i) / Convert.ToDouble(files.Length) * 100.0);

                if (progress % 10 == 0 && lastProgress != progress) {
                    lastProgress = progress;
                    Console.Write(progress + "% ");
                }
            }

            Console.WriteLine("DONE!");

            if (mode == MODE_STATS) {
                for (int i = 0; i < 100; i++) {
                    int count = 0;
                    if (ratings.ContainsKey(i)) {
                        count = ratings[i];
                    }
                    if (i <= 5 || count > 0) {
                        Console.WriteLine(i + " - " + count);
                    }
                }
            }
        }
    }
}
