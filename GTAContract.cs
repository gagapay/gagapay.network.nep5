using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class GagapaySmartContract : Framework.SmartContract
    {
        /*
        Name:         Gagapay network token
        Version:      1.0
        Author:       Gagapay Limited
        Email:        info@gagapay.network
        Description:  Gagapay network token
        Symbol:       GTA
        Precision:    8
        Supply:       1,000,000,000
        */
        public static string name() => "GagaPay network token";
        public static string symbol() => "GTA";
        public static readonly byte[] Owner = "AduTTdomtQUHfYR5W1sKSSg9jLAZrRVxJM".ToScriptHash();//translates 
        //to: 0xaf4b29a3ce72bceed2fec97595e60062eed71541., it's the ScriptHash of your public address ,you can view it in neo-gui(for example)
        public static byte decimals() => 8;//Decimals to display on user side
        private const ulong factor = 100000000; //decided by Decimals()
        //Since we don't want to deal with floating points sending 555 tokens with 8 Decimals (100000000) 
        //would actually mean that you want to  send 55500000000 tokens.
        private const ulong total_amount = 1000000000 * factor; //token amount

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;
        private static readonly byte[] approvePrefix = new byte[] { 0x1 };
        private static readonly byte[] balancePrefix = new byte[] { 0x2 };
        /// <summary>
        /// This smart contract implements NEP-5 methods and also the optional methods which currently don't have a NEP-5 standart,at the moment (they used to have ,now they don't) 
        /// </summary>
        /// <param name="operation">method name</param>
        /// <param name="args">Array of parameters that you wish to pass to the method</param>
        /// <returns></returns>
        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Owner.Length == 20)
                {
                    return Runtime.CheckWitness(Owner);
                }
                else if (Owner.Length == 33)
                {
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return deploy();
                if (operation == "totalSupply") return totalSupply();
                if (operation == "name") return name();
                if (operation == "symbol") return symbol();
                if (operation == "decimals") return decimals();
                if (operation == "approve") if (args.Length == 3) return approve((byte[])args[0], (byte[])args[1], (BigInteger)args[2]); else return NotifyErrorAndReturn0("argument count must be 3");
                if (operation == "allowance") if (args.Length == 2) return allowance((byte[])args[0], (byte[])args[1]); else return NotifyErrorAndReturn0("argument count must be atleast 2");
                if (operation == "transferFrom") if (args.Length == 4) return transferFrom((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3]); else return NotifyErrorAndReturn0("argument count must be atleast 4");
                if (operation == "transfer")
                {
                    if (args.Length != 3 || args[0] == null || ((byte[])args[0]).Length == 0 || args[1] == null || ((byte[])args[1]).Length == 0) return NotifyErrorAndReturnFalse("argument count must be 3 and they must not be null");
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return transfer(from, to, value, false);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1 || args[0] == null || ((byte[])args[0]).Length == 0) return NotifyErrorAndReturn0("argument count must be 1 and they must not be null");
                    byte[] account = (byte[])args[0];
                    return balanceOf(account);
                }
            }
            return NotifyErrorAndReturnFalse("Operation not found");
        }

        /// <summary>
        /// Can only be called by the Owner
        /// Puts total_amount value under "totalSupply" key and this function can be used only once ,unless the total amount is 0.
        /// </summary>
        /// <returns></returns>
        public static bool deploy()
        {
            if (!Runtime.CheckWitness(Owner)) //ensure that it is the owner calling this method
                return NotifyErrorAndReturnFalse("You are not the Owner of this Smart Contract");

            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");

            if (total_supply.Length != 0 || total_supply.AsBigInteger() != 0)
                return NotifyErrorAndReturnFalse("Looks like this method has been already used");

            Storage.Put(Storage.CurrentContext, balancePrefix.Concat(Owner), total_amount);
            Storage.Put(Storage.CurrentContext, "totalSupply", total_amount);
            Transferred(null, Owner, total_amount);
            return true;
        }

        /// <summary>
        /// Returns the Total Supply of Tokens
        /// </summary>
        /// <returns>total token supply(int)</returns>
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }
        /// <summary>
        ///   Checks the TransferFrom approval of two accounts.
        /// </summary>
        /// <param name="from">
        ///   The account which funds can be transfered from.
        /// </param>
        /// <param name="to">
        ///   The account which is granted usage of the account.
        /// </param>
        /// <returns>
        ///   The amount allocated for TransferFrom.
        /// </returns>
        public static BigInteger allowance(byte[] from, byte[] to)
        {
            if (!CheckAddress(from))
                return NotifyErrorAndReturn0("From value must not be empty and have size of 20");

            if (!CheckAddress(to))
                return NotifyErrorAndReturn0("To value must not be empty and have size of 20");

            return Storage.Get(Storage.CurrentContext, approvePrefix.Concat(from.Concat(to))).AsBigInteger();
        }
        /// <summary>
        ///   Approves another user to use the TransferFrom
        ///   function on the invoker's account.
        ///   TransferFrom 
        /// </summary>
        /// <param name="originator">
        ///   The contract invoker.
        /// </param>
        /// <param name="to">
        ///   The account to grant TransferFrom access to.
        ///   所批准的账户
        /// </param>
        /// <param name="amount">
        ///   The amount to grant TransferFrom access for.
        /// </param>
        /// <returns>
        ///   Transaction Successful?
        /// </returns>
        public static bool approve(byte[] originator, byte[] to, BigInteger amount)
        {
            if (originator == to)
                return NotifyErrorAndReturnFalse("Approving yourself shouldn't be necessary ");

            if (balanceOf(originator) < amount)
                return NotifyErrorAndReturnFalse("You don't have that kind of amount");

            if (!CheckAddress(to))
                return NotifyErrorAndReturnFalse("To value must not be empty and have size of 20");

            if (!CheckAddress(originator))
                return NotifyErrorAndReturnFalse("Originator value must not be empty and have size of 20");

            if (amount < 0)
                return NotifyErrorAndReturnFalse("Amount is lower than zero");

            if (!(Runtime.CheckWitness(originator)))
                return NotifyErrorAndReturnFalse("Originator isn't associated with this invoke");

            byte[] approveKey = approvePrefix.Concat(originator.Concat(to));
            if (!CheckAppAddress(approveKey))
                return NotifyErrorAndReturnFalse("Approval address is invalid");

            if (amount == 0)
            {
                Storage.Delete(Storage.CurrentContext, approveKey);
                return true;
            }
            Storage.Put(Storage.CurrentContext, approveKey, amount);
            return true;

        }
        /// <summary>
        ///   Transfers a balance from one account to another
        ///   on behalf of the account owner.
        /// </summary>
        /// <param name="originator">
        ///   The contract invoker.
        /// </param>
        /// <param name="from">
        ///   The account to transfer from.
        /// </param>
        /// <param name="to">
        ///   The account to transfer to.
        /// </param>
        /// <param name="amount">
        ///   The amount to transfer.
        /// </param>
        /// <returns>
        ///   Transaction successful?
        /// </returns>
        public static bool transferFrom(byte[] originator, byte[] from, byte[] to, BigInteger amountToSend)
        {
            if (from == to) return true;
            if (!CheckAddress(originator))
                return NotifyErrorAndReturnFalse("Originator value must not be empty and have size of 20");
            if (!CheckAddress(from))
                return NotifyErrorAndReturnFalse("From value must not be empty and have size of 20");
            if (!CheckAddress(to))
                return NotifyErrorAndReturnFalse("To value must not be empty and have size of 20");

            if (!(Runtime.CheckWitness(originator)))
                return NotifyErrorAndReturnFalse("Originator isn't associated with this invoke");

            if (amountToSend <= 0)
                return NotifyErrorAndReturnFalse("You need to send more than 0 tokens");

            byte[] approveKey = approvePrefix.Concat(from.Concat(originator));
            if (!CheckAppAddress(approveKey))
                return NotifyErrorAndReturnFalse("Approval address is invalid");

            BigInteger ownerAmount = balanceOf(from);

            if (ownerAmount < amountToSend)
                return NotifyErrorAndReturnFalse("Owner doesn't have this kind amount of tokens");

            if (ownerAmount <= 0)
                return NotifyErrorAndReturnFalse("Owner doesn't have positive balance");

            BigInteger allowedAmount = allowance(from, originator);

            if (allowedAmount < amountToSend)
                return NotifyErrorAndReturnFalse("You are trying to send more than you are allowed to");

            if (!(transfer(from, to, amountToSend, true)))//This does the actual transfer.
                return NotifyErrorAndReturnFalse("Failed to Transfer");



            BigInteger amountLeft = allowedAmount - amountToSend;

            if (amountLeft <= 0)//There should not be an situation where someone holds an negative balance ,but for safety sake I did add this.
            {
                Storage.Delete(Storage.CurrentContext, approveKey);
            }
            else
            {
                Storage.Put(Storage.CurrentContext, approveKey, amountLeft);
            }
            return true;
        }
        /// <summary>
        /// method that is always called when someone wants to transfer tokens.
        /// </summary>
        /// <param name="from"></param>
        /// The account to transfer from.
        /// <param name="to"></param>
        /// The account to transfer to.
        /// <param name="value"></param>
        /// amount that  will be sent
        /// <param name="transferFrom"></param>
        /// Checks if the function is called from TransferFrom method ,if so then there is no need to Check Witness.
        /// <returns>
        /// Returns if the transfer process has succeded or not.
        /// </returns>
        public static bool transfer(byte[] from, byte[] to, BigInteger value, bool transferFrom)
        {
            if (value < 0)
                return NotifyErrorAndReturnFalse("Negative transfers are not allowed");

            if (!CheckAddress(to))
                return NotifyErrorAndReturnFalse("To value must not be empty and have size of 20");
            if (!CheckAddress(from))
                return NotifyErrorAndReturnFalse("From value must not be empty and have size of 20");

            if (!transferFrom && !Runtime.CheckWitness(from)) return NotifyErrorAndReturnFalse("Owner of the wallet is not involved in this invoke");
            //if (value <= 0) return NotifyErrorAndReturnFalse("You must send more than 0 tokens");
            //if (from == to) return true;

            //Quote(26.07.2018) from https://github.com/neo-project/proposals/blob/d63dbec25cca1fdf1595ac609e54e588ff53214b/nep-5.mediawiki
            //If the method succeeds, it MUST fire the transfer event, and MUST return true, even if the amount is 0, or from and to are the same address. 
            if (value == 0 || from == to)
            {
                Transferred(from, to, value);
                return true;
            }
            byte[] toKey = balancePrefix.Concat(to);
            byte[] fromKey = balancePrefix.Concat(from);

            BigInteger from_value = Storage.Get(Storage.CurrentContext, fromKey).AsBigInteger();
            if (from_value < value) return NotifyErrorAndReturnFalse("Insufficient funds");
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, fromKey);
            else
                Storage.Put(Storage.CurrentContext, fromKey, from_value - value);

            BigInteger to_value = Storage.Get(Storage.CurrentContext, toKey).AsBigInteger();
            Storage.Put(Storage.CurrentContext, toKey, to_value + value);
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
        public static BigInteger balanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, balancePrefix.Concat(address)).AsBigInteger();
        }
        /// <summary>
        /// Used when we want to Notify Error for debbuging and return false
        /// </summary>
        /// <param name="value"></param>
        /// String that will be notified
        /// <returns>
        /// Allways False
        /// </returns>
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
        /// Checks if address is the right length
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool CheckAddress(byte[] address)
        {
            if (address == null || address.AsBigInteger() == 0 || address.Length != 20)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Checks if Approval address is the right length
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool CheckAppAddress(byte[] address)
        {
            if (address == null || address.AsBigInteger() == 0 || address.Length != 41)
            {
                return false;
            }
            return true;
        }
    }
}