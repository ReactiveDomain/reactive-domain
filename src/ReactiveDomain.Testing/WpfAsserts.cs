using ReactiveDomain.Testing;

namespace Xunit
{
    public partial class Assert
    {
        static partial void DispatchOtherThings()
        {
            DispatcherUtil.DoEvents();
        }
    }
}
