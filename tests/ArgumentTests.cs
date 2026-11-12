using System;
using System.IO;

internal static class ArgumentTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        foreach (string arg in args) Console.WriteLine("Argumento recibido: " + arg);
        bool reset = Program.WantsReset(args);
        Console.WriteLine("Reset reconocido: " + reset);
        if (!reset) return 1;
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "argument-history.xml");
        File.WriteAllText(path, "<posicion><punto x=\"1000\" y=\"874\" />"
            + "<punto x=\"1000\" y=\"875\" /></posicion>");
        using (DustWindow window = new DustWindow(reset, path)) { }
        if (File.Exists(path)) throw new Exception("El arranque no eliminó el histórico.");
        using (DustWindow reopened = new DustWindow(Program.WantsReset(new string[0]), path)) { }
        if (File.Exists(path)) throw new Exception("La reapertura restauró el histórico.");
        Console.WriteLine("OK: argumento real de Bash, reset y reapertura sin histórico.");
        return 0;
    }
}
