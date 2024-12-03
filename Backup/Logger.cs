using System;
using System.Windows.Controls;
using System.Windows.Media;

public class Logger
{
    private static Logger _instance;
    private ListBox _logBox;

    private Logger() { }

    public static Logger Instance => _instance ??= new Logger();

    public void Initialize(ListBox logBox)
    {
        _logBox = logBox;
    }

    public void Log(string message, LogType type)
    {
        _logBox?.Dispatcher.Invoke(() =>
        {
            var logEntry = new TextBlock
            {
                Text = $"{DateTime.Now:G} {message}",
                Margin = new System.Windows.Thickness(2),
                Foreground = GetLogBrush(type)
            };

            _logBox.Items.Add(logEntry);
        });
    }

    private Brush GetLogBrush(LogType type)
    {
        return type switch
        {
            LogType.Success => Brushes.Green,
            LogType.Warning => Brushes.DarkOrange,
            LogType.Error => Brushes.Red,
            _ => Brushes.Black
        };
    }
}

public enum LogType
{
    Success,
    Warning,
    Error
}