using System;
internal static class SnowCalibrationProbe
{
    static int Main(string[] args)
    {
        try { SnowCalibrationValidation.Run(); if(args.Length==0||args[0]!="--focused")SnowCalibrationValidation.MeasureFullSheet(); return 0; }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
