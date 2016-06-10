
namespace ONT
{
    /// <summary>
    /// Describes a WAN framing type, and the size of the header on the WAN circuit.
    /// </summary>
    public class WANFrameType
    {
        /// <summary>
        /// Type of media (Ethernet, PPP).  OK, this doesn't have the best name.  TODO: Change Media to Framing
        /// </summary>
        public string Media { get; set; }

        /// <summary>
        /// Size of the header and trailer in octets.
        /// </summary>
        public int Size { get; set; }

        public WANFrameType(string media, int size)
        {
            this.Media = media;
            this.Size = size;
        }
        public WANFrameType()
        { }
    }
}
