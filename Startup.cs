using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(CryptoCurrencyTrading.Startup))]
namespace CryptoCurrencyTrading
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
