using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace Lyhis {
    class Program {
        const int MODE_INVALID  = 0;
        const int MODE_RATE     = 1;
        const int MODE_FIX      = 2;

        static void Main(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("Lyhis.exe [command] [target directory] [rating]");
                Console.WriteLine("--");
                Console.WriteLine("Examples:");
                Console.WriteLine("Lyhis.exe rate \"C:\\presets\" 3");
                Console.WriteLine("Lyhis.exe fix \"C:\\presets\"");
                return;
            }

            int mode = MODE_INVALID;
            if (String.Compare(args[0], "rate", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_RATE;
            }
            else if (String.Compare(args[0], "fix", StringComparison.InvariantCultureIgnoreCase) == 0) {
                mode = MODE_FIX;
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

            var files = Directory.GetFiles(directory);

            Console.Write("Adjusting " + files.Length + " files ... ");

            double lastProgress = 0.0;

            for (int i = 0; i < files.Length; i++) {
                var file = files[i];

                if (file.EndsWith(".milk", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".mil", StringComparison.InvariantCultureIgnoreCase)) {
                    StringBuilder sb = new StringBuilder();
                    StreamReader sr = new StreamReader(file, Encoding.ASCII);
                    string line = null;
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

                int progress = (int)Math.Round(Convert.ToDouble(i) / Convert.ToDouble(files.Length) * 100.0);

                if (progress % 10 == 0 && lastProgress != progress) {
                    lastProgress = progress;
                    Console.Write(progress + "% ");
                }
            }

            Console.WriteLine("DONE!");
        }
    }
}
