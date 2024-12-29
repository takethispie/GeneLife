namespace Genelife.Domain.Interfaces;

public interface ISideEffectUsecase<out TResult, in TInput> {
    public TResult Execute(TInput input);
}