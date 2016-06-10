#!/usr/bin/perl
#
# udpechod.pl
#
# UDP echo server
#

# Written for IPv6-based testing.  Replace IO::Socket::INET6 with ::INET if you want IPv4 instead

use Socket;
use IO::Socket::INET6;
use strict;

my $sock = IO::Socket::INET6->new(LocalPort => 7, Proto=>'udp')
	|| die "Error: $!";

my $buf = "";

while(1) {
    my $remoteaddr;
    $remoteaddr = recv($sock, $buf, 1500, 0)	|| die "recv: $!";
    send($sock, $buf, 0, $remoteaddr)           || die "send: $!";
}