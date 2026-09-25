public interface ICollectable
{
    /// <summary>Returns true only for the one caller that actually got it.</summary>
    bool TryCollect();
}
