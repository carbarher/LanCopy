import hashlib

# Test MD5 hash generation
password = "aaa"
md5_hash = hashlib.md5(password.encode('utf-8')).hexdigest()
print(f"Password: {password}")
print(f"MD5 hash: {md5_hash}")
print(f"Expected: 47bce5c74f589f4867dbd57e9ca9f808")

# Test with the password from the example
password2 = "password"
md5_hash2 = hashlib.md5(password2.encode('utf-8')).hexdigest()
print(f"\nPassword: {password2}")
print(f"MD5 hash: {md5_hash2}")
print(f"Expected: 56f491c56340a6fa5c158863c6bfb39f")
