namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public struct RenderTargetHandle
    {
        public int id;

        public bool Equals(RenderTargetHandle other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is RenderTargetHandle && Equals((RenderTargetHandle) obj);
        }

        public override int GetHashCode()
        {
            return id;
        }

        public static readonly RenderTargetHandle BackBuffer = new RenderTargetHandle {id = -1};

        public static bool operator ==(RenderTargetHandle c1, RenderTargetHandle c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(RenderTargetHandle c1, RenderTargetHandle c2)
        {
            return !c1.Equals(c2);
        }
    }
}