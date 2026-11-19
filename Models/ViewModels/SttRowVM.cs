namespace DemoMVC.Models.ViewModels
{
    public class SttRowVM<T>
    {
        public int Stt { get; set; }
        public T Item { get; set; } = default!;
    }
}
