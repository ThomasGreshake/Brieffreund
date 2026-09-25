//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    public interface IEvent<T> where T : IEvent<T>
    {
        //Data ----------------------------------------

        private static event Action<T>? _onEvent;

        //Publics ----------------------------------------------------------

        public static void Register(Action<T> listener) => _onEvent += listener;

        public static void Unregister(Action<T> listener) => _onEvent -= listener;

        //Protected -----------------------------------------------------------

        protected static void Call(T eventInterface) => _onEvent?.Invoke(eventInterface);
    }
}