// Runs every test in CLASSNAME on a background thread; poll AppDomain data "testres".
System.AppDomain.CurrentDomain.SetData("testres", "running");
var th = new System.Threading.Thread(() =>
{
    try
    {
        var asm = System.Reflection.Assembly.Load("Skyforge.Simulation.Tests");
        var t = asm.GetType("Skyforge.Simulation.Tests.CLASSNAME");
        int pass = 0; var fails = new System.Collections.Generic.List<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var m in t.GetMethods())
        {
            var cases = new System.Collections.Generic.List<object[]>();
            foreach (var a in m.GetCustomAttributes(true))
            {
                var an = a.GetType().Name;
                if (an == "TestAttribute") cases.Add(new object[0]);
                if (an == "TestCaseAttribute") cases.Add((object[])a.GetType().GetProperty("Arguments").GetValue(a));
            }
            foreach (var args in cases)
            {
                var inst = System.Activator.CreateInstance(t);
                try { m.Invoke(inst, args); pass++; }
                catch (System.Reflection.TargetInvocationException e)
                {
                    var ie = e.InnerException;
                    if (ie.GetType().Name == "SuccessException") { pass++; continue; }
                    if (ie.GetType().Name == "IgnoreException" || ie.GetType().Name == "InconclusiveException") continue;
                    fails.Add(m.Name + "(" + string.Join(",", args) + "): " + ie.Message.Replace("\n", " ").Replace("\"", "'"));
                }
            }
        }
        
        System.AppDomain.CurrentDomain.SetData("testres", "CLASSNAME pass=" + pass + " fail=" + fails.Count + " in " + sw.ElapsedMilliseconds / 1000 + "s :: " + string.Join(" || ", fails));
    }
    catch (System.Exception ex) { System.AppDomain.CurrentDomain.SetData("testres", "crashed: " + ex.Message); }
}, 64 * 1024 * 1024);
th.Start();
return "started";
