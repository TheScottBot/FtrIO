namespace FtrIO.Interfaces
{
    using System;
    using Enums;

    public interface IFeatureToggle <T>
    {
        ToggleStatus GetToggleState(IToggleParser parser, string toggleKey);
        void ExecuteMethodIfToggleOn(Action methodToRun, string? keyName = null);
        T ExecuteMethodIfToggleOn(Func<T> methodToRun, string? keyName = null);
        Task ExecuteMethodIfToggleOnAsync(Func<Task> methodToRun, string? keyName = null);
        Task<TResult> ExecuteMethodIfToggleOnAsync<TResult>(Func<Task<TResult>> methodToRun, string? keyName = null);
    }
}