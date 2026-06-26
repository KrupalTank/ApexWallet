namespace ApexWallet.Api.Security
{
    public interface ICryptoService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}