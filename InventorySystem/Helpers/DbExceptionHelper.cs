using System;
using Microsoft.Data.SqlClient;

namespace InventorySystem.Helpers
{
    internal static class DbExceptionHelper
    {
        public static bool IsUniqueConstraintViolation(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    (sqlException.Number == 2627 || sqlException.Number == 2601))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
