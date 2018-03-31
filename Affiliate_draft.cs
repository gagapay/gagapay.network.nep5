using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;
using System.Numerics;

namespace NeoContract2
{
    public class Contract1 : SmartContract
    {
        public static readonly byte[] Owner1 = "Abdeg1wHpSrfjNzH5edGTabi5jdD9dvncX".ToScriptHash();
        public static readonly byte[] Owner2 = "AYfJPwjy5jM9MX3AnRS8EmAeETuH6yV5Q7".ToScriptHash();
        public static readonly byte[] affiliateParentPostFix = new byte[1] { 1 };
        public static readonly BigInteger[] affiliateLevelPercentage = new BigInteger[] { 1, 2, 3, 4, 5 };

        /*
                Name: Gagapay network token

                Version: 1.0

                Author: Gagapay Limited

                Email: info@gagapay.com

                Description: Gagapay network token

                Symbol: GTA

                Precision: 8

                Supply: 1,000,000,000
        */
        //Token Settings
        public static string Name() => "GagaPay network affiliate token";
        public static string Symbol() => "GATA";
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        //Since we don't want to deal with floating points sending 555 tokens with 8 Decimals (100000000) 
        //would actually mean that you want to  send 55500000000 tokens.
        private const ulong total_amount = 1000000000 * factor; //token amount

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;
        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Owner1.Length == 20)
                {
                    // if param Owner is script hash
                    return Runtime.CheckWitness(Owner1);
                }
                else if (Owner1.Length == 33)
                {
                    // if param Owner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner1);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "decimals") return Decimals();
                if (operation == "transfer")
                {
                    if (args.Length != 3 || args[0] == null || ((byte[])args[0]).Length == 0 || args[1] == null || ((byte[])args[1]).Length == 0) return NotifyErrorAndReturnFalse("argument count must be 3 and they must not be null");
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1 || args[0] == null || ((byte[])args[0]).Length == 0) return NotifyErrorAndReturn0("argument count must be 1 and they must not be null");
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
            }
            return NotifyErrorAndReturnFalse("Operation not found!");
        }

        public static bool PayAffiliateLevel5(byte[] from, byte[] to, BigInteger amount)
        {
            if (!Runtime.CheckWitness(from))
                NotifyErrorAndReturnFalse("From is not associated with this invoke");
            if (CheckIfAddressIsValid(from))
                return NotifyErrorAndReturnFalse("From address is not valid!");
            BigInteger fromAmount = BalanceOf(from);
            if (fromAmount < amount)
                return NotifyErrorAndReturnFalse("Insufficient funds");
            if (CheckIfAddressIsValid(to))
                return NotifyErrorAndReturnFalse("To address is not valid!");
            if (amount <= 0)
                return NotifyErrorAndReturnFalse("You need to send more than 0");
            BigInteger distributedAmount;

            byte[] parent = to;

            for (int i = 0; i < 5; i++)
            {
                distributedAmount = amount / 100 * affiliateLevelPercentage[i];
                amount = amount - distributedAmount;
                if (amount >= 0)
                {
                    if (distributedAmount > 0)
                    {
                        parent = GetAffiliatesParent(parent);
                        if (parent != null)
                        {
                            if (!Transfer(from, parent, distributedAmount))
                            {
                                Runtime.Notify("Couldn't transfer the funds at level ", i);
                            }
                        }
                        else
                        {
                            Runtime.Notify("Couldn't find parent at level", i + 1);
                            break;
                        }
                    }
                    else
                    {
                        Runtime.Notify("Distributed amount is not over 0 on level", i + 1);
                        break;
                    }
                }
            }
            if (!Transfer(from, to, amount))
                Runtime.Notify("Could execute the last Transfer");
            return true;
        }

        public static bool PutAffiliatesParent(byte[] user, byte[] parent)
        {
            if (!Runtime.CheckWitness(getCurrentAdmin()))
                return NotifyErrorAndReturnFalse("Only administrator can perform this action!");
            if (CheckIfAddressIsValid(user))
                return NotifyErrorAndReturnFalse("User address is not valid!");
            if (CheckIfAddressIsValid(parent))
                return NotifyErrorAndReturnFalse("Parent  address is not valid!");

            Storage.Put(Storage.CurrentContext, user.Concat(affiliateParentPostFix), parent);
            return true;

        }
        public static byte[] GetAffiliatesParent(byte[] user)
        {
            if (CheckIfAddressIsValid(user))
                return Storage.Get(Storage.CurrentContext, user.Concat(affiliateParentPostFix));

            return null;
        }
        public static bool CheckIfAddressIsValid(byte[] address)
        {
            if (address != null && address.Length == 20)
                return true;
            return false;
        }
        public static bool Deploy()
        {
            if (Runtime.CheckWitness(Owner1) || Runtime.CheckWitness(Owner2))
            {
                byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");

                if (total_supply.Length != 0)
                    return NotifyErrorAndReturnFalse("Looks like this method has been allready used");

                Storage.Put(Storage.CurrentContext, Owner1, total_amount);
                Storage.Put(Storage.CurrentContext, "totalSupply", total_amount);

                Storage.Put(Storage.CurrentContext, "admin", Owner2);
                return true;
            }
            return NotifyErrorAndReturnFalse("You are not auhorized to perform such action!");

        }
        public static bool RewriteAdmin(byte[] admin)
        {
            if (Runtime.CheckWitness(Owner1))
            {
                if (CheckIfAddressIsValid(admin))
                {
                    Storage.Put(Storage.CurrentContext, "admin", admin);
                    return true;
                }
                return NotifyErrorAndReturnFalse("The address passed is not an valid address");
            }
            return NotifyErrorAndReturnFalse("You are not auhorized to perform such action!");

        }
        public static byte[] getCurrentAdmin()
        {
            return Storage.Get(Storage.CurrentContext, "admin");
        }
        public static bool NotifyErrorAndReturnFalse(string value)
        {
            Runtime.Notify(value);
            return false;
        }
        /// <summary>
        /// Used when we want to Notify Error for debbuging and return 0
        /// </summary>
        /// <param name="value"></param>
        /// String that will be notified
        /// <returns>
        /// Allways 0
        /// </returns>
        public static int NotifyErrorAndReturn0(string value)
        {
            Runtime.Notify(value);
            return 0;
        }
        /// <summary>
        /// Returns the Total Supply of Tokens
        /// </summary>
        /// <returns></returns>
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }
        /// <summary>
        /// function that is always called when someone wants to transfer tokens.
        /// </summary>
        /// <param name="from"></param>
        /// wallet from which we send the tokens.
        /// <param name="to"></param>
        /// wallet to which we send the tokens.
        /// <param name="value"></param>
        /// amount that we will send
        /// <param name="transferFrom"></param>
        /// Checks if the function is called from TransferFrom methods ,if so then there is no need to Check Witness.
        /// <returns>
        /// Returns if the tranfer process has succeded or not.
        /// </returns>
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return NotifyErrorAndReturnFalse("Try to send more than 0 tokens");
            if (!Runtime.CheckWitness(from)) return NotifyErrorAndReturnFalse("Owner of the wallet is not involved in this invoke");
            if (from == to) return true;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return NotifyErrorAndReturnFalse("Insufficient funds"); ;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            Transferred(from, to, value);
            return true;
        }
        /// <summary>
        /// Get the account balance of another account with address,uses wallets address scripthash.
        /// </summary>
        /// <param name="address"></param>
        /// The Adress that we want to know blance of
        /// <returns>
        /// Amount of tokens associated with this address
        /// </returns>
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }
    }
}
