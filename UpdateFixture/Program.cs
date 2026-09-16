class Program
{
    static void Main(string[] args)
    {
        if(args.Length==0) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"fixture-restarted"),"ok"); return; }
        File.WriteAllText(args[0]+".started","ok");
        for(int i=0;i<600&&!File.Exists(args[0]+".ready");i++) Thread.Sleep(100);
    }
}
