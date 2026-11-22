using System;
using System.Collections.Generic;
using CopyIt;

public class MoveItSelectionFixture { public List<int> SelectedEntities {get;}=new List<int>{4,7,4}; }
public class MoveItBrokenFixture { public List<int> SelectedEntities=>throw new InvalidOperationException(); }
internal static class MoveItCompatibilityTests
{
    internal static void Run()
    {
        void Check(bool ok){if(!ok)throw new Exception("Move It snapshot contract failed");}
        var tool=new MoveItSelectionFixture();
        Check(MoveItCompatibility.TrySnapshot<int>(tool,out var snapshot,out _)&&snapshot.Length==2);
        tool.SelectedEntities.Clear();
        Check(snapshot.Length==2&&Array.IndexOf(snapshot,4)>=0&&Array.IndexOf(snapshot,7)>=0);
        Check(MoveItCompatibility.TrySnapshot<int>(tool,out snapshot,out _)&&snapshot.Length==0);
        Check(!MoveItCompatibility.TrySnapshot<int>(new object(),out _,out _));
        Check(!MoveItCompatibility.TrySnapshot<string>(new MoveItSelectionFixture(),out _,out _));
        Check(!MoveItCompatibility.TrySnapshot<int>(new MoveItBrokenFixture(),out _,out _));
        Check(!MoveItCompatibility.TrySnapshot<int>(null,out _,out _));
        Console.WriteLine("PASS: Move It public selection snapshot fixtures (no game execution).");
    }
}
