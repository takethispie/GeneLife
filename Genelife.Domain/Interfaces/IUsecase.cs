namespace Genelife.Domain.Interfaces;

public interface IUsecase<in T> {
    public void Execute(T input);
}