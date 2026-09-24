using HomeLibrary.Forms;

namespace HomeLibrary;

internal static class Program
{
    /// <summary>
    /// Главная точка входа приложения "Домашняя библиотека".
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}