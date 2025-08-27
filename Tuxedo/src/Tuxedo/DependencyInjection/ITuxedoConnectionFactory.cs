using System.Data;

namespace Tuxedo.DependencyInjection
{
    public interface ITuxedoConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}