using System;
using System.Data;

namespace Tuxedo.DependencyInjection
{
    public class TuxedoConnectionFactory : ITuxedoConnectionFactory
    {
        private readonly Func<IDbConnection> _connectionFactory;

        public TuxedoConnectionFactory(Func<IDbConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IDbConnection CreateConnection()
        {
            return _connectionFactory();
        }
    }
}