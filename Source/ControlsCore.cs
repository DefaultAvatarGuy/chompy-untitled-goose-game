namespace TinyRexPlaytime
{
    [System.Flags]
    internal enum Commands { None = 0, CycleSize = 1, ToggleFire = 2, Reset = 4, Fart = 8 }

    // Engine-independent control logic, shared with the executable verification harness.
    internal sealed class ControlsCore
    {
        private readonly ushort[] previous = new ushort[4];
        private static readonly float[] Factors = { 0.65f, 1f, 1.65f };
        public int SizeIndex { get; private set; }
        public bool FireEnabled { get; private set; }
        public float Factor { get { return Factors[SizeIndex]; } }
        public ControlsCore() { SizeIndex = 1; FireEnabled = true; }
        public Commands PollPad(int index, bool connected, ushort current)
        {
            if (index < 0 || index >= previous.Length) return Commands.None;
            if (!connected) { previous[index] = 0; return Commands.None; }
            ushort down = (ushort)(current & ~previous[index]);
            previous[index] = current;
            Commands result = (down & 0x0080) != 0 ? Commands.Fart : Commands.None;
            if ((current & 0x0300) != 0x0300) return result;
            if ((down & 0x8000) != 0) result |= Commands.CycleSize;
            if ((down & 0x4000) != 0) result |= Commands.ToggleFire;
            if ((down & 0x2000) != 0) result |= Commands.Reset;
            if ((down & 0x1000) != 0) result |= Commands.Fart; // Alternate LB+RB+A
            return result;
        }
        public Commands Apply(Commands command)
        {
            // Reset wins if multiple face buttons are pressed together.
            if ((command & Commands.Reset) != 0) { SizeIndex = 1; FireEnabled = false; return Commands.Reset; }
            Commands result = command & Commands.Fart;
            if ((command & Commands.CycleSize) != 0) { SizeIndex = (SizeIndex + 1) % Factors.Length; return result | Commands.CycleSize; }
            if ((command & Commands.ToggleFire) != 0) { FireEnabled = !FireEnabled; return result | Commands.ToggleFire; }
            return result;
        }
    }
}
