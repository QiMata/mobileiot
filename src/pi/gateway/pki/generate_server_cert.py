"""Generate a server certificate signed by the gateway CA for OPC UA.

Creates:
    <out_dir>/certs/server_cert.pem
    <out_dir>/private/server_key.pem

Usage::

    sudo python -m gateway.pki.generate_server_cert \\
        [--out-dir /etc/mobileiot/opcua] \\
        [--hostname raspberrypi]
"""

from __future__ import annotations

import argparse
import os
import socket
from datetime import datetime, timedelta, timezone

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

DEFAULT_OUT_DIR = "/etc/mobileiot/opcua"
APPLICATION_URI = "urn:qimata:mobileiot:gateway"


def generate_server_cert(
    out_dir: str = DEFAULT_OUT_DIR,
    hostname: str | None = None,
    days: int = 365,
) -> tuple[str, str]:
    """Generate a server certificate signed by the CA."""
    hostname = hostname or socket.gethostname()

    # Load CA
    ca_cert_path = os.path.join(out_dir, "certs", "ca_cert.pem")
    ca_key_path = os.path.join(out_dir, "private", "ca_key.pem")

    with open(ca_cert_path, "rb") as f:
        ca_cert = x509.load_pem_x509_certificate(f.read())
    with open(ca_key_path, "rb") as f:
        ca_key = serialization.load_pem_private_key(f.read(), password=None)

    # Generate server key
    server_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    subject = x509.Name(
        [
            x509.NameAttribute(NameOID.COMMON_NAME, hostname),
            x509.NameAttribute(NameOID.ORGANIZATION_NAME, "QiMata"),
        ]
    )

    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(ca_cert.subject)
        .public_key(server_key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(datetime.now(timezone.utc))
        .not_valid_after(datetime.now(timezone.utc) + timedelta(days=days))
        .add_extension(
            x509.SubjectAlternativeName(
                [
                    x509.DNSName(hostname),
                    x509.DNSName("localhost"),
                    x509.UniformResourceIdentifier(APPLICATION_URI),
                ]
            ),
            critical=False,
        )
        .add_extension(
            x509.BasicConstraints(ca=False, path_length=None), critical=True
        )
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                key_encipherment=True,
                content_commitment=False,
                data_encipherment=False,
                key_agreement=False,
                key_cert_sign=False,
                crl_sign=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .add_extension(
            x509.ExtendedKeyUsage(
                [x509.oid.ExtendedKeyUsageOID.SERVER_AUTH]
            ),
            critical=False,
        )
        .sign(ca_key, hashes.SHA256())  # type: ignore[arg-type]
    )

    certs_dir = os.path.join(out_dir, "certs")
    private_dir = os.path.join(out_dir, "private")
    os.makedirs(certs_dir, mode=0o755, exist_ok=True)
    os.makedirs(private_dir, mode=0o700, exist_ok=True)

    cert_path = os.path.join(certs_dir, "server_cert.pem")
    key_path = os.path.join(private_dir, "server_key.pem")

    with open(cert_path, "wb") as f:
        f.write(cert.public_bytes(serialization.Encoding.PEM))

    with open(key_path, "wb") as f:
        f.write(
            server_key.private_bytes(
                serialization.Encoding.PEM,
                serialization.PrivateFormat.TraditionalOpenSSL,
                serialization.NoEncryption(),
            )
        )
    os.chmod(key_path, 0o600)

    # Copy CA cert to trust directory
    trust_dir = os.path.join(out_dir, "trust")
    os.makedirs(trust_dir, mode=0o755, exist_ok=True)
    trust_ca = os.path.join(trust_dir, "ca_cert.pem")
    if not os.path.exists(trust_ca):
        with open(ca_cert_path, "rb") as src, open(trust_ca, "wb") as dst:
            dst.write(src.read())

    print(f"Server certificate: {cert_path}")
    print(f"Server private key: {key_path}")
    print(f"CA cert in trust:   {trust_ca}")
    return cert_path, key_path


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate gateway server certificate")
    parser.add_argument(
        "--out-dir", default=DEFAULT_OUT_DIR, help="Output directory"
    )
    parser.add_argument("--hostname", default=None, help="Server hostname")
    parser.add_argument("--days", type=int, default=365, help="Validity in days")
    args = parser.parse_args()
    generate_server_cert(args.out_dir, args.hostname, args.days)


if __name__ == "__main__":
    main()
