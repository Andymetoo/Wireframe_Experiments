internal static class IceCalibrationProbe
{
    static int Main()
    {
        try { IceCalibrationValidation.Run();return 0; }
        catch(System.Exception error) { System.Console.Error.WriteLine(error);return 1; }
    }
}
