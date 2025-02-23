using HomeEnergy.Policies;

namespace HomeEnergy.Services
{
    public class PoliciesManager
    {
        private IServiceScopeFactory _scopeFactory;

        public PoliciesManager(IServiceScopeFactory scopeFactory, IEnumerable<IPolicyFactory> policyFactories)
        {
            _scopeFactory = scopeFactory;
        }
    }
}
