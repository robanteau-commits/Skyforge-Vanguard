// Runs the named SimulationTests (NAMES = "A","B") on a background thread; poll AppDomain data "testres".
System.AppDomain.CurrentDomain.SetData("testres", "running");
var th = new System.Threading.Thread(() =>
{
    var res = new System.Text.StringBuilder();
    var t = System.Reflection.Assembly.Load("Skyforge.Simulation.Tests").GetType("Skyforge.Simulation.Tests.SimulationTests");
    foreach (var name in new[] { NAMES })
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { t.GetMethod(name).Invoke(System.Activator.CreateInstance(t), null); res.Append(name + " PASS " + sw.ElapsedMilliseconds + "ms; "); }
        catch (System.Exception e) { res.Append(name + " FAIL: " + (e.InnerException ?? e).Message.Replace("\n", " ").Replace("\"", "'") + "; "); }
    }
    System.AppDomain.CurrentDomain.SetData("testres", res.ToString());
}, 64 * 1024 * 1024);
th.Start();
return "started";
