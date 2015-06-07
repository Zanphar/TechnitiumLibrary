﻿/*
Technitium Library
Copyright (C) 2015  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TechnitiumLibrary.IO;

namespace TechnitiumLibrary.Security.Cryptography
{
    public enum SymmetricEncryptionAlgorithm : byte
    {
        Unknown = 0,
        DES = 1,
        RC2 = 2,
        TripleDES = 3,
        RC4 = 4,
        Rijndael = 5
    }

    public sealed class SymmetricCryptoKey : WriteStream, IDisposable
    {
        #region variables

        SymmetricAlgorithm _symAlgo;
        SymmetricEncryptionAlgorithm _cryptoAlgo;

        #endregion

        #region constructor

        public SymmetricCryptoKey(SymmetricEncryptionAlgorithm cryptoAlgo, int keySize, PaddingMode padding = PaddingMode.ISO10126)
        {
            _symAlgo = SymmetricAlgorithm.Create(cryptoAlgo.ToString());
            _symAlgo.KeySize = keySize;
            _symAlgo.Padding = padding;
            _symAlgo.Mode = CipherMode.CBC;
            _symAlgo.GenerateKey();
            _symAlgo.GenerateIV();

            _cryptoAlgo = cryptoAlgo;
        }

        public SymmetricCryptoKey(SymmetricEncryptionAlgorithm cryptoAlgo, byte[] key, byte[] IV = null, PaddingMode padding = PaddingMode.ISO10126)
        {
            _symAlgo = SymmetricAlgorithm.Create(cryptoAlgo.ToString());
            _symAlgo.KeySize = key.Length * 8;
            _symAlgo.Padding = padding;
            _symAlgo.Mode = CipherMode.CBC;
            _symAlgo.Key = key;

            if (IV == null)
                _symAlgo.GenerateIV();
            else
                _symAlgo.IV = IV;

            _cryptoAlgo = cryptoAlgo;
        }

        public SymmetricCryptoKey(Stream s)
        {
            ReadFrom(new BinaryReader(s));
        }

        public SymmetricCryptoKey(BinaryReader bR)
        {
            ReadFrom(bR);
        }

        #endregion

        #region IDisposable

        bool _disposed = false;

        ~SymmetricCryptoKey()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool Disposing)
        {
            if (!_disposed)
            {
                _symAlgo.Clear();
                _disposed = true;
            }
        }

        #endregion

        #region private

        private void ReadFrom(BinaryReader bR)
        {
            if (Encoding.ASCII.GetString(bR.ReadBytes(2)) != "SK")
                throw new CryptoException("Invalid SymmetricCryptoKey format.");

            byte version = bR.ReadByte();

            switch (version) //version
            {
                case 1:
                case 2:
                    //algorithm
                    _cryptoAlgo = (SymmetricEncryptionAlgorithm)bR.ReadByte();
                    _symAlgo = SymmetricAlgorithm.Create(_cryptoAlgo.ToString());

                    //key
                    _symAlgo.Key = bR.ReadBytes(bR.ReadByte());

                    //IV
                    _symAlgo.IV = bR.ReadBytes(bR.ReadByte());

                    //padding
                    if (version == 1)
                        _symAlgo.Padding = PaddingMode.ISO10126;
                    else
                        _symAlgo.Padding = (PaddingMode)bR.ReadByte();

                    _symAlgo.Mode = CipherMode.CBC;
                    break;

                default:
                    throw new CryptoException("SymmetricCryptoKey format version not supported.");
            }
        }

        #endregion

        #region public

        public override void WriteTo(BinaryWriter bW)
        {
            bW.Write(Encoding.ASCII.GetBytes("SK")); //format
            bW.Write((byte)2); //version

            //algorithm
            bW.Write((byte)_cryptoAlgo);

            //key
            byte[] Key = _symAlgo.Key;
            bW.Write(Convert.ToByte(Key.Length));
            bW.Write(Key);

            //IV
            byte[] IV = _symAlgo.IV;
            bW.Write(Convert.ToByte(IV.Length));
            bW.Write(IV);

            //padding
            bW.Write(Convert.ToByte(_symAlgo.Padding));
        }

        #endregion

        #region Symmetric Crypto Key Methods

        public ICryptoTransform GetEncryptor()
        {
            return _symAlgo.CreateEncryptor();
        }

        public ICryptoTransform GetDecryptor()
        {
            return _symAlgo.CreateDecryptor();
        }

        public CryptoStream GetCryptoStreamWriter(Stream s)
        {
            return new CryptoStream(s, _symAlgo.CreateEncryptor(), CryptoStreamMode.Write);
        }

        public CryptoStream GetCryptoStreamReader(Stream s)
        {
            return new CryptoStream(s, _symAlgo.CreateDecryptor(), CryptoStreamMode.Read);
        }

        public void Encrypt(Stream clearText, Stream cipherText, int bufferSize = 128 * 1024)
        {
            using (CryptoStream cW = new CryptoStream(cipherText, _symAlgo.CreateEncryptor(), CryptoStreamMode.Write))
            {
                OffsetStream.StreamCopy(clearText, cW, bufferSize);
                cW.FlushFinalBlock();
            }
        }

        public void Decrypt(Stream cipherText, Stream clearText, int bufferSize = 128 * 1024)
        {
            OffsetStream.StreamCopy(new CryptoStream(cipherText, _symAlgo.CreateDecryptor(), CryptoStreamMode.Read), clearText, bufferSize);
        }

        #endregion

        #region properties

        public SymmetricEncryptionAlgorithm Algorithm
        { get { return _cryptoAlgo; } }

        public int BlockSize
        { get { return _symAlgo.BlockSize; } }

        public int KeySize
        { get { return _symAlgo.KeySize; } }

        public byte[] IV
        { get { return _symAlgo.IV; } }

        #endregion
    }
}