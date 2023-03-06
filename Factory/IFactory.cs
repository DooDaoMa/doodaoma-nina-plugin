namespace Doodaoma.NINA.Doodaoma.Factory {
    public interface IFactory<out T> {
        T Create();
    }
}