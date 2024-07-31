using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Buffers.Text;

namespace S84Account.Service
{
    public static class JWT
    {
        private class Key
        {
            public static readonly string PRIVATE = "PRIVATE_KEY";
            public static readonly string PUBLIC = "PUBLIC_KEY";
            public static readonly string Header = "eyJhbGciOiJFUzM4NCIsInR5cCI6IkpXVCJ9.";
            public string? DBase64 { get; set; }
            public string? QxBase64 { get; set; }
            public string? QyBase64 { get; set; }
        }

        public static readonly string ISSUER = Util.GetEnv("ISSUER", Util.RandomString(5));
        private static readonly ECDsa _privateKey = LoadKey(Key.PRIVATE);
        private static readonly ECDsa _publicKey = LoadKey(Key.PUBLIC);

        public static string GenerateES384(string content, string issuer, string audience, DateTime? expires = null)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, content)]),
                Expires = expires ?? DateTime.UtcNow.AddDays(1),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(_privateKey), SecurityAlgorithms.EcdsaSha384)
            };

            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor)).Replace(Key.Header, "");
        }
        public static IIdentity? ValidateES384(string token, string issuer, string audience)
        {
            if (string.IsNullOrEmpty(token)) return null;

            JwtSecurityTokenHandler tokenHandler = new();

            try
            {
                IPrincipal principal = tokenHandler.ValidateToken(Key.Header + token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new ECDsaSecurityKey(_publicKey),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);
                return principal.Identity;
            }
            catch
            {
                return null;
            }
        }
        public static bool IsTokenExpiringSoon(string token, int thresholdInMinutes)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            JwtSecurityToken jwtToken = tokenHandler.ReadJwtToken(token);

            Claim? expClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Exp);
            if (expClaim == null)
            {
                return true;
            }

            DateTimeOffset exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value)).UtcDateTime;
            DateTime currentUtcTime = DateTime.UtcNow;

            return exp <= currentUtcTime.AddMinutes(thresholdInMinutes);
        }
        private static string GenerateES384Key()
        {
            string Key = "";
            ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            ECParameters privateKey = ecdsa.ExportParameters(true);

            if (privateKey.D != null && privateKey.Q.X != null && privateKey.Q.Y != null)
            {
                Key += Convert.ToBase64String(privateKey.D);
                Key += Convert.ToBase64String(privateKey.Q.X);
                Key += Convert.ToBase64String(privateKey.Q.Y);
            }
            return Key;
        }

        private static ECDsa LoadKey(string keyName)
        {
            int length = 64;
            string base64Key = Util.GetEnv(keyName, GenerateES384Key());
            Key key = new()
            {
                DBase64 = Key.PRIVATE == keyName ? base64Key[0..length] : "",
                QxBase64 = Key.PRIVATE == keyName ? base64Key[length..(length * 2)] : base64Key[0..length],
                QyBase64 = Key.PRIVATE == keyName ? base64Key[(length * 2)..(length * 3)] : base64Key[length..(length * 2)]
            };

            byte[] d = Convert.FromBase64String(key.DBase64);
            byte[] qx = Convert.FromBase64String(key.QxBase64);
            byte[] qy = Convert.FromBase64String(key.QyBase64);

            ECParameters ecParameters = new()
            {
                Curve = ECCurve.NamedCurves.nistP384,
                Q = new ECPoint
                {
                    X = qx,
                    Y = qy
                }
            };
            if (Key.PRIVATE == keyName) ecParameters.D = d;

            return ECDsa.Create(ecParameters);
        }

        private static string CreatePublicKeyFromPrivateKey(ECDsa privateKey)
        {
            string key = "";
            ECParameters privateKeyParams = privateKey.ExportParameters(false);
            ECParameters publicKeyParams = new()
            {
                Curve = privateKeyParams.Curve,
                Q = privateKeyParams.Q
            };
            if (publicKeyParams.Q.X != null && publicKeyParams.Q.Y != null)
            {
                key += Convert.ToBase64String(publicKeyParams.Q.X);
                key += Convert.ToBase64String(publicKeyParams.Q.Y);
            }
            return key;
        }

    }
}
