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
                @"/Users/Jakob/Desktop/Dia",
                "JPG",
                true,
                true,
                true,
                false,
                "Prefix_",
                1
            );
            process.Start();
        }
    }
}
