using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool reset;
        try { reset = WantsReset(args); }
        catch (ArgumentException error)
        {
            MessageBox.Show(error.Message, "Mota de polvo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try
        {
            Application.Run(new DustWindow(reset));
        }
        catch (IOException error) { ShowStartupError(error); }
        catch (UnauthorizedAccessException error) { ShowStartupError(error); }
    }

    internal static bool WantsReset(string[] args)
    {
        foreach (string arg in args)
        {
            // Git Bash/MSYS translates the Unix-looking /R switch into the drive root R:/.
            // Accept that exact conversion, never arbitrary paths ending in R.
            if (!string.Equals(arg, "/R", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(arg, "-r", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(arg, "--reset", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(arg, "R:/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(arg, @"R:\", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Opción no reconocida: " + arg
                    + "\n\nPara borrar el histórico, usa /R, -r o --reset.");
        }
        return args.Length != 0;
    }

    private static void ShowStartupError(Exception error)
    {
        MessageBox.Show("No se ha podido borrar el histórico. La aplicación se cerrará.\n\n"
            + error.Message, "Mota de polvo", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal sealed class PositionHistory
{
    private readonly string path;
    public readonly List<Point> Points = new List<Point>();
    public string Monitor = "";

    public PositionHistory(string path) { this.path = path; }

    public Point? Latest
    {
        get { return Points.Count == 0 ? (Point?)null : Points[Points.Count - 1]; }
    }

    public bool Add(Point point)
    {
        if (Latest.HasValue && Latest.Value == point) return false;
        Points.Add(point);
        return true;
    }

    public void Reset()
    {
        // Delete the persisted history itself. Do not depend on replacing its metadata,
        // and do not claim success or clear memory if the deletion fails.
        File.Delete(path);
        Points.Clear();
        Monitor = "";
    }

    public void Load()
    {
        XmlDocument document = new XmlDocument();
        document.XmlResolver = null;
        document.Load(path);
        XmlElement root = document.DocumentElement;
        Points.Clear();
        if (root == null) return;
        Monitor = root.GetAttribute("monitor");
        XmlNodeList positions = root.SelectNodes("punto");
        if (positions.Count == 0)
            ReadPoint(root); // Preserve the position saved by the original version.
        else
            foreach (XmlElement position in positions) ReadPoint(position);
    }

    private void ReadPoint(XmlElement element)
    {
        int x, y;
        if (int.TryParse(element.GetAttribute("x"), out x)
            && int.TryParse(element.GetAttribute("y"), out y) && x >= 0 && y >= 0)
            Add(new Point(x, y));
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporaryPath = path + ".tmp";
        XmlWriterSettings options = new XmlWriterSettings();
        options.Indent = true;
        using (XmlWriter writer = XmlWriter.Create(temporaryPath, options))
        {
            writer.WriteStartElement("posicion");
            writer.WriteAttributeString("monitor", Monitor);
            foreach (Point point in Points)
            {
                writer.WriteStartElement("punto");
                writer.WriteAttributeString("x", point.X.ToString());
                writer.WriteAttributeString("y", point.Y.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        if (File.Exists(path)) File.Replace(temporaryPath, path, null);
        else File.Move(temporaryPath, path);
    }
}

internal static class DustCursor
{
    internal const int Center = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public IntPtr Mask;
        public IntPtr Color;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]
    internal static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    internal static Bitmap CreateImage()
    {
        Bitmap bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Pen pen = new Pen(Color.Black, 1))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.DrawEllipse(pen, Center - 3, Center - 3, 6, 6);
            graphics.DrawLine(pen, Center, 2, Center, Center - 6);
            graphics.DrawLine(pen, Center, Center + 6, Center, 30);
            graphics.DrawLine(pen, 2, Center, Center - 6, Center);
            graphics.DrawLine(pen, Center + 6, Center, 30, Center);
        }
        return bitmap;
    }

    internal static IntPtr CreateHandle()
    {
        using (Bitmap bitmap = CreateImage())
        {
            IntPtr icon = bitmap.GetHicon();
            IconInfo info = new IconInfo();
            try
            {
                if (!GetIconInfo(icon, out info)) throw new Win32Exception();
                info.IsIcon = false;
                info.HotspotX = Center;
                info.HotspotY = Center;
                IntPtr cursor = CreateIconIndirect(ref info);
                if (cursor == IntPtr.Zero) throw new Win32Exception();
                return cursor;
            }
            finally
            {
                if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
                if (info.Color != IntPtr.Zero) DeleteObject(info.Color);
                DestroyIcon(icon);
            }
        }
    }
}

internal sealed class DustWindow : Form
{
    private const int Radius = 10;
    private const int HistoryRadius = 3;
    private readonly PositionHistory history;
    private readonly bool resetSucceeded;
    private Point? markedPoint { get { return history.Latest; } }
    private bool saveFailed;
    private bool showHistory;
    private bool historyKeyDown;
    private IntPtr cursorHandle;
    private Cursor dustCursor;

    public DustWindow(bool reset) : this(reset, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MotaPantalla", "posicion.xml")) { }

    internal DustWindow(bool reset, string settingsPath)
    {
        history = new PositionHistory(settingsPath);
        if (reset)
        {
            history.Reset();
            resetSucceeded = true;
        }
        Text = "Mota de polvo";
        BackColor = Color.White;
        ForeColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        KeyPreview = true;
        cursorHandle = DustCursor.CreateHandle();
        dustCursor = new Cursor(cursorHandle);
        Cursor = dustCursor;
        Font = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);

        Screen targetScreen = Screen.FromPoint(Cursor.Position);
        try
        {
            if (!reset) history.Load();
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.DeviceName == history.Monitor)
                {
                    targetScreen = screen;
                    break;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (XmlException) { }

        Bounds = targetScreen.Bounds;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && dustCursor != null)
        {
            dustCursor.Dispose();
            dustCursor = null;
            DustCursor.DestroyCursor(cursorHandle);
            cursorHandle = IntPtr.Zero;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        history.Add(e.Location);
        SavePosition();
        Invalidate();
    }

    private void SavePosition()
    {
        try
        {
            history.Monitor = Screen.FromControl(this).DeviceName;
            history.Save();
            saveFailed = false;
        }
        catch (IOException) { saveFailed = true; }
        catch (UnauthorizedAccessException) { saveFailed = true; }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        if (e.KeyCode == Keys.H)
        {
            if (!historyKeyDown)
            {
                showHistory = !showHistory;
                Invalidate();
            }
            historyKeyDown = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.H) historyKeyDown = false;
        base.OnKeyUp(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        historyKeyDown = false;
        base.OnDeactivate(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (markedPoint.HasValue)
        {
            Point point = markedPoint.Value;
            // Coordinates and radius are physical pixels; the manifest disables DPI virtualization.
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using (Pen pen = new Pen(Color.Black, 1))
            {
                if (showHistory)
                {
                    if (history.Points.Count > 1)
                        e.Graphics.DrawLines(pen, history.Points.ToArray());
                    foreach (Point previous in history.Points)
                        e.Graphics.DrawEllipse(pen, previous.X - HistoryRadius,
                            previous.Y - HistoryRadius, HistoryRadius * 2, HistoryRadius * 2);
                }
                e.Graphics.DrawEllipse(pen, point.X - Radius, point.Y - Radius,
                    Radius * 2, Radius * 2);
            }
        }

        string caption = markedPoint.HasValue
            ? string.Format("X: {0}   Y: {1}", markedPoint.Value.X, markedPoint.Value.Y)
            : (resetSucceeded ? "Histórico borrado · " : "")
                + "Haz clic sobre la mota · H: histórico · Esc para salir";
        if (showHistory) caption += string.Format(" · Histórico: {0} puntos", history.Points.Count);
        if (markedPoint.HasValue && !ClientRectangle.Contains(markedPoint.Value))
            caption += " · Fuera de esta pantalla: haz clic de nuevo";
        if (saveFailed) caption += " · No se ha podido guardar el histórico";

        Rectangle footer = new Rectangle(12, Math.Max(0, ClientSize.Height - 38),
            Math.Max(0, ClientSize.Width - 24), 26);
        TextRenderer.DrawText(e.Graphics, caption, Font, footer, Color.Black,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}
