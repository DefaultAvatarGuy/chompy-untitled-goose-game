using System;
using TinyRexPlaytime;
class Tests
{
 static int n;
 static void Check(bool pass,string text){n++;if(!pass)throw new Exception(text);}
 static void Main(){
  var c=new ControlsCore();Check(c.Factor==1f&&c.FireEnabled,"default behavior retained");
  Check(c.PollPad(0,true,0x80)==Commands.Fart,"dedicated right-stick click");
  Check(c.PollPad(0,true,0x80)==Commands.None,"held stick does not repeat");
  c.PollPad(0,true,0);Check(c.PollPad(0,true,0x1300)==Commands.Fart,"alternate LB RB A");
  c.PollPad(0,true,0);Check(c.PollPad(0,true,0x1000)==Commands.None,"A alone unchanged");
  Check(c.Apply(Commands.Fart)==Commands.Fart&&c.FireEnabled&&c.Factor==1f,"fart does not change fire or size");
  c.Apply(Commands.Reset);Check(!c.FireEnabled&&c.Factor==1f,"reset behavior");
  Check(c.Apply(Commands.Fart)==Commands.Fart&&!c.FireEnabled,"fart works with fire disabled");
  c.PollPad(1,true,0x8300);Check(c.PollPad(1,true,0x8300)==Commands.None,"paused held combo not queued");
  c.PollPad(0,true,0);Check(c.PollPad(0,true,0x8300)==Commands.CycleSize,"size combo retained");
  Check(c.PollPad(2,true,0x4300)==Commands.ToggleFire,"fire combo retained");
  Check(c.PollPad(3,true,0x2300)==Commands.Reset,"reset combo retained");
  for(int i=0;i<30000;i++){c.Apply(Commands.CycleSize);Check(c.Factor==.65f||c.Factor==1f||c.Factor==1.65f,"bounded sizes");}
  Check(c.Factor==1f,"no size drift");
  Check(c.Apply(Commands.Reset|Commands.CycleSize|Commands.ToggleFire|Commands.Fart)==Commands.Reset&&c.Factor==1f&&!c.FireEnabled,"reset priority suppresses accidental fart");
  Check(c.PollPad(4,true,0x80)==Commands.None,"invalid slot ignored");
  c.PollPad(0,false,0);Check(c.PollPad(0,true,0)==Commands.None,"disconnect cleared");
  Console.WriteLine("PASS: "+n+" controls checks.");
 }
}
