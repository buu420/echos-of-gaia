namespace Memoria.Accessibility.AudioDescriptions
{
    internal enum JumpRopePrompt { None, Start, Pass }

    internal sealed class JumpRopeCue
    {
        private object _room;
        private JumpRopePrompt _lastPrompt;
        private int _lastJumpCount;
        private int _lastFrame = -1;

        internal static JumpRopePrompt Identify(int field, int actor, int offset, string script)
        {
            switch (field)
            {
                case 103: // Alexandria Square
                    if (actor == 9 && offset == 215 && script == "388b3f65caf4af5a272110a06db8d71503de69c6643470456ca4537bf515cdf2")
                        return JumpRopePrompt.Pass;
                    if (actor == 30 && offset == 1094 && script == "0e72508bc17c135bed7a794f53ea81941c31cadc590c6832cae3a3e85e87f45d")
                        return JumpRopePrompt.Start;
                    break;
                case 2456: // Alexandria Steeple, Vivi or Eiko
                    if (actor == 6 && offset == 193 && script == "0e747dc56a53988699dd8167fca78000d2521adb0b7d390963a11fd7c59a3b25")
                        return JumpRopePrompt.Pass;
                    if (actor == 16 && offset == 439 && script == "7767d40a0ab196cba250ccef9380d659e42a6ee804a37737cd078a562b035706" ||
                        actor == 21 && offset == 443 && script == "f02b4f94615b9e40e392d7b545c3d3d1d9d66a6cf016115f3e9f4ddba7055e1f")
                        return JumpRopePrompt.Start;
                    break;
            }
            return JumpRopePrompt.None;
        }

        internal bool Observe(object room, JumpRopePrompt prompt, int jumpCount, int frame, bool canPlay)
        {
            if (!canPlay || room == null || prompt == JumpRopePrompt.None)
                return false;

            if (!ReferenceEquals(_room, room))
            {
                _room = room;
                _lastPrompt = JumpRopePrompt.None;
                _lastFrame = -1;
            }

            if (_lastFrame == frame || (_lastPrompt == prompt && _lastJumpCount == jumpCount))
                return false;

            _lastPrompt = prompt;
            _lastJumpCount = jumpCount;
            _lastFrame = frame;
            return true;
        }
    }
}
