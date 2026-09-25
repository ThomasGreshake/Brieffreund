//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal class MailAddress : Address
    {
        //Data -------------------------------------------------------------

        internal readonly int MailAmount;

        //Setup -------------------------------------------------------------

        internal static MailAddress Create(IStreetMap map, Street street, string fullNumber, int mailAmount, StreetPath path, Vector2 position)
        {
            MailAddress address = new(map, street, fullNumber, mailAmount, path, position);
            IOnAddressCreated.Call(address);
            return address;
        }

        private MailAddress(IStreetMap map, Street street, string fullNumber, int mailAmount, StreetPath path, Vector2 position)
            : base(map, street, fullNumber, path, position)
        {
            MailAmount = mailAmount;
        }
    }
}