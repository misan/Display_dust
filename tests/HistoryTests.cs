using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class HistoryTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Check(!Program.WantsReset(new string[0]), "Sin argumentos debe recuperarse el histórico.");
        foreach (string option in new string[] { "/R", "/r", "-r", "--reset", "R:/", @"R:\" })
            Check(Program.WantsReset(new string[] { option }), "Debe reconocerse el reset: " + option);
        foreach (string invalid in new string[] { "--rest", @"C:\datos\R", "R:/datos" })
        {
            bool rejected = false;
            try { Program.WantsReset(new string[] { invalid }); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "No deben ignorarse opciones desconocidas ni resetear por rutas arbitrarias.");
        }
        string path = Path.GetFullPath(args[0]);
        File.WriteAllText(path, "<posicion x=\"120\" y=\"230\" monitor=\"DISPLAY1\" />");
        PositionHistory original = new PositionHistory(path);
        original.Load();
        Check(original.Points.Count == 1 && original.Latest == new Point(120, 230),
            "La coordenada antigua debe convertirse en el primer punto.");
        Check(original.Monitor == "DISPLAY1", "Debe conservarse el monitor.");
        Check(!original.Add(new Point(120, 230)), "Un clic repetido no debe duplicar el punto.");
        original.Add(new Point(140, 250));
        original.Add(new Point(120, 230));
        original.Add(new Point(0, 0));
        original.Save();

        PositionHistory restored = new PositionHistory(path);
        restored.Load();
        Check(restored.Points.Count == 4, "Debe guardarse el recorrido completo.");
        for (int index = 0; index < original.Points.Count; index++)
            Check(restored.Points[index] == original.Points[index], "Debe conservarse el orden.");
        Check(restored.Latest == new Point(0, 0), "Debe recuperarse el último punto, incluido el origen.");
        Check(restored.Monitor == "DISPLAY1", "Debe recuperarse el monitor.");

        restored.Reset();
        Check(!File.Exists(path) && restored.Points.Count == 0 && !restored.Latest.HasValue,
            "El reset debe borrar el archivo y los puntos en memoria.");
        restored.Reset();
        restored.Add(new Point(5, 6));
        restored.Save();
        PositionHistory reset = new PositionHistory(path);
        reset.Load();
        Check(reset.Points.Count == 1 && reset.Latest == new Point(5, 6),
            "Después del reset debe comenzar un histórico independiente.");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        bool refused = false;
        try { reset.Reset(); }
        catch (UnauthorizedAccessException) { refused = true; }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        Check(refused && reset.Points.Count == 1 && File.Exists(path),
            "Un borrado fallido debe notificarse sin fingir un reset correcto.");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Bitmap cursor = DustCursor.CreateImage())
        {
            int center = DustCursor.Center;
            for (int x = center - 1; x <= center + 1; x++)
                for (int y = center - 1; y <= center + 1; y++)
                    Check(cursor.GetPixel(x, y).A == 0, "El centro del cursor debe ser transparente.");
            Check(cursor.GetPixel(center, center - 3).ToArgb() == Color.Black.ToArgb()
                && cursor.GetPixel(center + 3, center).ToArgb() == Color.Black.ToArgb(),
                "El círculo del cursor debe tener radio de tres píxeles.");
        }
        File.WriteAllText(path, "<posicion><punto x=\"200\" y=\"200\" />"
            + "<punto x=\"240\" y=\"200\" /></posicion>");
        using (DustWindow before = new DustWindow(false, path))
            CheckHistoryDrawing(before, true);
        using (DustWindow cleared = new DustWindow(true, path))
            CheckHistoryDrawing(cleared, false);
        Check(!File.Exists(path), "El arranque con /R debe eliminar el histórico persistido.");
        using (DustWindow reopened = new DustWindow(false, path))
            CheckHistoryDrawing(reopened, false);
        Console.WriteLine("OK: migración, persistencia, reset, fallo de borrado, H tras reabrir y cursor transparente.");
    }

    private static void CheckHistoryDrawing(DustWindow window, bool expectedHistory)
    {
        Check(window.Cursor.HotSpot == new Point(DustCursor.Center, DustCursor.Center),
            "El punto de clic debe estar en el centro transparente del cursor.");
        window.Bounds = new Rectangle(0, 0, 400, 400);
        MethodInfo keyDown = typeof(DustWindow).GetMethod("OnKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        keyDown.Invoke(window, new object[] { new KeyEventArgs(Keys.H) });
        using (Bitmap bitmap = new Bitmap(400, 400))
        {
            window.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 400));
            Check(bitmap.GetPixel(220, 200).ToArgb()
                == (expectedHistory ? Color.Black : Color.White).ToArgb(),
                "H debe mostrar la línea antes del reset y ningún histórico tras /R o reapertura.");
        }
    }
}
