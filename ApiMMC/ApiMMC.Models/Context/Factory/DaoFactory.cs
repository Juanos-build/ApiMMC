using ApiMMC.Models.Context.Interfaces;

namespace ApiMMC.Models.Context.Factory
{
    public abstract class DaoFactory
    {
        #region Transactional Interfaces

        public abstract ITransactionDao GetTransactionDao();
        public abstract IReaddingDao GetReaddingDao();

        public virtual TDao GetDao<TDao>() where TDao : class
        {
            if (typeof(TDao) == typeof(IReaddingDao)) return (TDao)GetReaddingDao();

            throw new InvalidOperationException($"DAO de tipo {typeof(TDao).Name} no está registrado en DaoFactory");
        }

        #endregion
    }
}
