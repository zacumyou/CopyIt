using System;
using CopyIt;

int checks=0;
void Near(float actual,float expected,string name,float tolerance=0.0001f)
{
    if(Math.Abs(actual-expected)>tolerance || float.IsNaN(actual))throw new Exception(name+": "+actual+" != "+expected);
    checks++;
}
void Check(bool condition,string name) { if(!condition)throw new Exception(name);checks++; }

// Translate the complete road profile rigidly; destination terrain does not reshape it.
Near(PlacementMath.Height(0,0,4,false),4,"ground road start 0 to 4");
Near(PlacementMath.Height(11,0,4,false),15,"ground road end 11 to 15");
foreach(float y in new[]{0f,2f,5f,11f})
    Near(PlacementMath.Height(y,0,4,false)-y,4,"road curve controls share vertical translation");
Near(PlacementMath.Height(1.5f,1.5f,3f,false),3f,"example prop");
Near(PlacementMath.Height(1.7f,1.5f,3f,false),3.2f,"example building");
Near(PlacementMath.Height(1.5f,1.5f,3f,true),1.5f,"absolute prop");
Near(PlacementMath.Height(1.7f,1.5f,3f,true),1.7f,"absolute building");
Near(PlacementMath.Height(-4,-4,-12,false),-12,"below sea level minimum");
Near(PlacementMath.Height(-2,-4,-12,false),-10,"below sea level delta");
Near(PlacementMath.Height(20,20,0,false),0,"single item");
Near(PlacementMath.Height(20,20,100,true),20,"absolute ignores terrain");
Near(PlacementMath.Height(0,-2,8,false),10,"uneven destination does not independently snap");
foreach(var invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
    for(int field=0;field<3;field++)
    {
        bool threw=false;
        try { PlacementMath.Height(field==0?invalid:1,field==1?invalid:1,field==2?invalid:1,false); }
        catch(ArgumentException) {threw=true;}
        Check(threw,"reject non-finite field "+field);
    }
Check(PlacementMath.InBox(5,5,10,10,0,0),"reverse drag");
Check(PlacementMath.InBox(0,10,0,0,10,10),"box border");
Check(!PlacementMath.InBox(11,5,0,0,10,10),"outside box");
Check(PlacementMath.InBox(0,0,0,0,0,0),"zero box");
var random=new Random(4102);
for(int trial=0;trial<10000;trial++)
{
    float minimum=(float)(random.NextDouble()*1000-500);
    float first=minimum+(float)(random.NextDouble()*100);
    float second=minimum+(float)(random.NextDouble()*100);
    float ground=(float)(random.NextDouble()*1000-500);
    var a=PlacementMath.Height(first,minimum,ground,false);
    var b=PlacementMath.Height(second,minimum,ground,false);
    Near(a-b,first-second,"pairwise relative heights",0.0002f);
    Near(PlacementMath.Height(minimum,minimum,ground,false),ground,"minimum lands on terrain");
    Near(PlacementMath.Height(first,minimum,ground,true),first,"absolute stability");
}
var square=new[]{new Point2(0,0),new Point2(10,0),new Point2(10,10),new Point2(0,10)};
Check(SelectionMath.SimplePolygon(square),"simple polygon");
Check(SelectionMath.Intersects(square,new Rect2(9,4,12,6)),"partial object overlap");
Check(SelectionMath.Intersects(square,new Rect2(-1,4,11,6)),"edges cross without corners inside");
Check(SelectionMath.Intersects(square,new Rect2(-1,-1,11,11)),"polygon inside object bounds");
Check(SelectionMath.Intersects(square,new Rect2(10,10,11,11)),"touching boundary");
Check(!SelectionMath.Intersects(square,new Rect2(11,11,12,12)),"outside polygon");
Check(!SelectionMath.SimplePolygon(new[]{new Point2(0,0),new Point2(10,10),new Point2(0,10),new Point2(10,0)}),"self crossing");
var concave=new[]{new Point2(0,0),new Point2(10,0),new Point2(10,4),new Point2(4,4),new Point2(4,10),new Point2(0,10)};
Check(SelectionMath.SimplePolygon(concave),"concave allowed");
Check(!SelectionMath.Intersects(concave,new Rect2(6,6,8,8)),"concave notch excluded");
Check(SelectionMath.Intersects(concave,new Rect2(2,6,3,8)),"concave arm included");
Near(SelectionMath.NormalizeAngle(-1),359,"negative wheel wrap");
Near(SelectionMath.NormalizeAngle(361),1,"positive wheel wrap");
var quarter=SelectionMath.Rotate(3,4,90);Near(quarter.X,4,"yaw x");Near(quarter.Y,-3,"yaw z");
for(int trial=0;trial<10000;trial++){
    float x=(float)random.NextDouble()*100,z=(float)random.NextDouble()*100,angle=(float)random.NextDouble()*720-360;
    var rotated=SelectionMath.Rotate(x,z,angle);var restored=SelectionMath.Rotate(rotated.X,rotated.Y,-angle);
    Near(restored.X,x,"rotation inverse x",0.00005f);Near(restored.Y,z,"rotation inverse z",0.00005f);
}
// Regression: a large building's projected bounds can overlap while its origin is outside.
Check(SelectionMath.Intersects(square,new Rect2(9,2,15,8)),"legacy broad bounds overlap");
Check(!SelectionMath.Contains(square,new Point2(12,5)),"outside building origin rejected");
Check(!new Rect2(0,0,10,10).Contains(new Point2(12,5)),"box also rejects outside origin");
Check(SelectionMath.Contains(square,new Point2(5,5)),"inside origin accepted");
Check(SelectionMath.Contains(square,new Point2(10,5)),"origin on boundary accepted");
Check(!SelectionMath.Contains(concave,new Point2(6,6)),"concave notch origin rejected");
Array.Reverse(concave);Check(!SelectionMath.Contains(concave,new Point2(6,6)),"reverse winding notch");
for(int trial=0;trial<10000;trial++){
    float x=(float)random.NextDouble()*200-100,y=(float)random.NextDouble()*200-100,z=(float)random.NextDouble()*200-100;
    float ex=(float)random.NextDouble()*10,ey=(float)random.NextDouble()*10,ez=(float)random.NextDouble()*10;
    var plane=new SelectionPlane((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*20-10);
    float maximum=float.MinValue;
    for(int corner=0;corner<8;corner++){
        float px=x+((corner&1)==0?-ex:ex),py=y+((corner&2)==0?-ey:ey),pz=z+((corner&4)==0?-ez:ez);
        maximum=Math.Max(maximum,plane.X*px+plane.Y*py+plane.Z*pz+plane.W);
    }
    if(Math.Abs(maximum+0.01f)>0.0001f)Check(plane.Overlaps(x,y,z,ex,ey,ez)==(maximum>=-0.01f),"tree plane pruning agrees with eight corners");
}
// A perspective sub-frustum for screen x/z,y/z in [-0.2,0.2], with depth [1,100].
var planes=new[]{new SelectionPlane(1,0,.2f,0),new SelectionPlane(-1,0,.2f,0),new SelectionPlane(0,1,.2f,0),new SelectionPlane(0,-1,.2f,0),new SelectionPlane(0,0,1,-1),new SelectionPlane(0,0,-1,100)};
bool Frustum(float x,float y,float z,float extent){foreach(var p in planes)if(!p.Overlaps(x,y,z,extent,extent,extent))return false;return true;}
Check(Frustum(0,0,20,1),"tree node inside sub-frustum");
Check(!Frustum(20,0,20,1),"tree node outside selection pruned");
Check(!Frustum(0,0,-10,1),"node behind camera pruned");
Check(Frustum(0,0,1,2),"node crossing near plane retained");
var nodes=new NodeMapping<int>();
Check(nodes.Add(1,101),"new junction");Check(nodes.Add(1,101),"shared junction stays shared");
Check(!nodes.Add(1,102),"shared junction split rejected");Check(!nodes.Add(2,101),"separate junction merge rejected");
Check(!nodes.Add(3,3),"source node reuse rejected");Check(nodes.Add(2,102),"second fresh junction");
for(int i=0;i<10000;i++){
    float x=(float)random.NextDouble()*100,z=(float)random.NextDouble()*100,y=(float)random.NextDouble()*20;
    float angle=(float)random.NextDouble()*360,t=(float)random.NextDouble();
    var a=new Point2(x,z);var b=new Point2(x+3,z+8);var c=new Point2(x+10,z-4);var d=new Point2(x+20,z);
    Point2 Bezier(Point2 p0,Point2 p1,Point2 p2,Point2 p3){float u=1-t;return new Point2(u*u*u*p0.X+3*u*u*t*p1.X+3*u*t*t*p2.X+t*t*t*p3.X,u*u*u*p0.Y+3*u*u*t*p1.Y+3*u*t*t*p2.Y+t*t*t*p3.Y);}
    Point2 Map(Point2 p){var r=SelectionMath.Rotate(p.X,p.Y,angle);return new Point2(r.X+100,r.Y-75);}
    var first=Map(Bezier(a,b,c,d));var second=Bezier(Map(a),Map(b),Map(c),Map(d));
    Near(first.X,second.X,"Bezier shape under group rotation x",.0001f);Near(first.Y,second.Y,"Bezier shape under group rotation z",.0001f);
    Near(PlacementMath.Height(y,0,30,false)-PlacementMath.Height(y+2,0,30,false),-2,"network elevation differences",.00001f);
}var polygon32=new Point2[32];for(int i=0;i<32;i++)polygon32[i]=new Point2(500+(float)Math.Cos(i*Math.PI/16)*400,500+(float)Math.Sin(i*Math.PI/16)*400);
int oldHits=0,newHits=0;var timer=System.Diagnostics.Stopwatch.StartNew();
for(int repeat=0;repeat<5;repeat++)for(int i=0;i<20000;i++){float x=i%200*5,y=i/200*10;if(SelectionMath.Intersects(polygon32,new Rect2(x,y,x+4,y+4)))oldHits++;}
double oldMs=timer.Elapsed.TotalMilliseconds/5;timer.Restart();
for(int repeat=0;repeat<5;repeat++)for(int i=0;i<20000;i++){float x=i%200*5,y=i/200*10;if(SelectionMath.Contains(polygon32,new Point2(x+2,y+2)))newHits++;}
Console.WriteLine($"Geometry benchmark / 20000 candidates, 32 vertices: old bounds {oldMs:F2} ms; origin test {timer.Elapsed.TotalMilliseconds/5:F2} ms. Desktop .NET; excludes game tree and ECS. hits={oldHits}/{newHits}");
// Double-click decisions use the state BEFORE the first (immediate toggle) click.
var cycle=new FilterClicks();int mask=255;
foreach(int bit in new[]{1,2,4,8,16,32,64,128}){
    cycle.Reset();mask=255;
    mask=cycle.Apply(mask,bit,1,255,out var twice);Check(!twice,"first filter click");
    mask=cycle.Apply(mask,bit,1.1,255,out twice);Check(twice&&mask==bit,"solo filter");
    mask=cycle.Apply(mask,bit,2,255,out twice);
    mask=cycle.Apply(mask,bit,2.1,255,out twice);Check(twice&&mask==(255&~bit),"all except solo filter");
    mask=cycle.Apply(mask,bit,3,255,out twice);
    mask=cycle.Apply(mask,bit,3.1,255,out twice);Check(twice&&mask==bit,"except returns to solo");
}
var assetSet=new AssetSamples<int>();
Check(assetSet.Add(10,0)&&assetSet.Add(30,0),"sample A and C");
Check(assetSet.Contains(10,0)&&assetSet.Contains(30,0)&&!assetSet.Contains(20,0),"asset union excludes B");
Check(!assetSet.Add(10,0)&&assetSet.Count==2,"duplicate sample deduplicated");
assetSet.Add(40,100);Check(!assetSet.Contains(40,101),"netlane and decal subprefab identity");
Check(!assetSet.Contains(11,0),"same type different asset excluded");
assetSet.Clear();Check(assetSet.Count==0&&!assetSet.Contains(10,0),"clear samples");
Near(SelectionMath.WheelAngle(0,1),.5f,"wheel half degree");
Near(SelectionMath.WheelAngle(0,-1),359.5f,"wheel negative wrap");
Near(SelectionMath.WheelAngle(359.5f,1),0,"wheel full turn wrap");
Near(SelectionMath.WheelAngle(12.3f,2),13.3f,"wheel keeps free drag angle");
Check(SelectionMath.Underground(true,0,0),"tunnel flag independent of height");
Check(SelectionMath.Underground(false,0,-8),"underground entrance segment");
Check(SelectionMath.Underground(false,-8,-8),"underground segment");
Check(!SelectionMath.Underground(false,0,0),"ground network");
Check(!SelectionMath.Underground(false,8,8),"elevated network");
Check(!SelectionMath.Underground(false,-.001f,0),"elevation float noise");
var clicks=new FilterClicks();
Check(!clicks.IsDoubleClick(8,0),"first type click toggles");
Check(clicks.IsDoubleClick(8,.20),"fast same type isolates");
Check(!clicks.IsDoubleClick(8,.22),"third click starts a new pair");
Check(!clicks.IsDoubleClick(8,.60),"slow same type toggles");
Check(!clicks.IsDoubleClick(64,.65),"different type does not isolate");
Check(clicks.IsDoubleClick(64,.80),"paths can isolate");
clicks.Reset();Check(!clicks.IsDoubleClick(1,1),"reset first click");
clicks.Reset();Check(!clicks.IsDoubleClick(1,1.1),"intervening action breaks double click");
Check(!clicks.IsDoubleClick(1,.5),"backward clock does not isolate");
for(int trial=0;trial<100;trial++){
    float a=(float)random.NextDouble()*100,h1=(float)random.NextDouble()*100,h2=(float)random.NextDouble()*100,d=(float)random.NextDouble()*100;
    var controls=PlacementMath.FitTerrainCurve(a,h1,h2,d);
    Near((8*a+12*controls.Item1+6*controls.Item2+d)/27,h1,"terrain curve at one third",.0001f);
    Near((a+6*controls.Item1+12*controls.Item2+8*d)/27,h2,"terrain curve at two thirds",.0001f);
}
Console.WriteLine($"PASS: {checks} assertions. These are logic tests, not an in-game test.");

