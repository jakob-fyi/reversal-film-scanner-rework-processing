using System;
using System.Drawing;
using System.IO;

namespace reversal_film_scanner_rework_processing
{

    class Program
    {
        static void Main(string[] args)
        {
            Process process = new Process(
                @"/Users/Jakob/Geschichte/Ortsarchiv/Dia Scan/Progress/BEH010",
                "JPG",
                true,
                true,
                true,
                true,
                1
            );

            process.Start();
        }
    }
}
