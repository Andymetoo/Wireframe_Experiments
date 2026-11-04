internal static class FreedomProbe
{
    static void Main(string[] args)
    {
        try
        {
            if(args.Length>0&&args[0]=="route")FreedomValidation.FullRun();
            else FreedomValidation.Run();
        }
        catch(System.Exception error){System.Console.Error.WriteLine(error);System.Environment.Exit(1);}
    }
}
