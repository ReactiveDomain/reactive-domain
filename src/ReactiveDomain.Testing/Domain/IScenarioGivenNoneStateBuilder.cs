namespace ReactiveDomain.Testing
{
    public interface IScenarioGivenNoneStateBuilder
    {
        IScenarioWhenStateBuilder When(object command);
    }
}