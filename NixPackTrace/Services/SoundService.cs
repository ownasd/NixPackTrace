using System.Media;

namespace NixPackTrace.Services
{
    public static class SoundService
    {
        public static void PlaySuccess()
        {
            SystemSounds.Asterisk.Play();
        }

        public static void PlayError()
        {
            SystemSounds.Hand.Play();
        }
    }
}
