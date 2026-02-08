"""Generate a self-signed Certificate Authority for the OPC UA gateway.

Creates:
    <out_dir>/certs/ca_cert.pem
    <out_dir>/private/ca_key.pem

Usage::

    sudo python -m gateway.pki.generate_ca [--out-dir /etc/mobileiot/opcua]
"""

from __future__ import annotations

import argparse
import os
from datetime import datetime, timedelta, timezone

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

DEFAULT_OUT_DIR = "/etc/mobileiot/opcua"


def generate_ca(
    out_dir: str = DEFAULT_OUT_DIR,
    cn: str = "MobileIoT Gateway CA",
    days: int = 3650,
) -> tuple[str, str]:
    """Generate a self-signed CA certificate and private key."""
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    subject = issuer = x509.Name(
        [
            x509.NameAttribute(NameOID.COMMON_NAME, cn),
            x509.NameAttribute(NameOID.ORGANIZATION_NAME, "QiMata"),
        ]
    )

    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(issuer)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(datetime.now(timezone.utc))
        .not_valid_after(datetime.now(timezone.utc) + timedelta(days=days))
        .add_extension(
            x509.BasicConstraints(ca=True, path_length=None), critical=True
        )
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                key_cert_sign=True,
                crl_sign=True,
                content_commitment=False,
                key_encipherment=False,
                data_encipherment=False,
                key_agreement=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .sign(key, hashes.SHA256())
    )

    certs_dir = os.path.join(out_dir, "certs")
    private_dir = os.path.join(out_dir, "private")
    os.makedirs(certs_dir, mode=0o755, exist_ok=True)
    os.makedirs(private_dir, mode=0o700, exist_ok=True)

    cert_path = os.path.join(certs_dir, "ca_cert.pem")
    key_path = os.path.join(private_dir, "ca_key.pem")

    with open(cert_path, "wb") as f:
        f.write(cert.public_bytes(serialization.Encoding.PEM))

    with open(key_path, "wb") as f:
        f.write(
            key.private_bytes(
                serialization.Encoding.PEM,
                serialization.PrivateFormat.TraditionalOpenSSL,
                serialization.NoEncryption(),
            )
        )
    os.chmod(key_path, 0o600)

    print(f"CA certificate: {cert_path}")
    print(f"CA private key: {key_path}")
    return cert_path, key_path


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate gateway CA")
    parser.add_argument(
        "--out-dir", default=DEFAULT_OUT_DIR, help="Output directory"
    )
    parser.add_argument("--cn", default="MobileIoT Gateway CA", help="Common name")
    parser.add_argument("--days", type=int, default=3650, help="Validity in days")
    args = parser.parse_args()
    generate_ca(args.out_dir, args.cn, args.days)


if __name__ == "__main__":
    main()
