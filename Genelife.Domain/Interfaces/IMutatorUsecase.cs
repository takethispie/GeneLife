namespace Genelife.Domain.Interfaces;

public interface IMutatorUsecase<T> {
    public T Execute(T input);
}