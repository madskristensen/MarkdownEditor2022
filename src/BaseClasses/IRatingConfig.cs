namespace BaseClasses
{
    public interface IRatingConfig
    {
        int RatingIncrements { get; set; }

        Task SaveAsync();
    }
}
