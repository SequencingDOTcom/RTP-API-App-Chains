#!/usr/bin/perl -w

use strict;
use warnings;
use AppChains;
use Data::Dumper;

my $chains = AppChains->new("<your token goes here>", "api.sequencing.com");

# single result retrieval
my $chainsResult = $chains->getReport("StartApp", "Chain9", "227680");

# batch result retrieval
my %chainsSpec = ("Chain9" => "227680", "Chain88" => "227680");
my $out = $chains->getBatchReport("StartAppBatch", \%chainsSpec);

foreach (keys %$out)
{
	my $cr = $out->{$_};
	print "-> Chain ID: " . $_ . "\n";
	handleAppChainResult($cr);
	print "\n\n";
}


sub handleAppChainResult
{
	my ($chainsResult) = @_;
	
	if ($chainsResult->isSucceeded()) {
		print "\tRequest has succeeded\n";
	} else {
		print "\tRequest has failed\n";
	}

	foreach (@{$chainsResult->getResults()})
	{
		my $type = $_->getValue()->getType();
	
		if ($type == ResultType->TEXT)
		{
			my $v = $_->getValue();
			print sprintf("\t-> text result type %s = %s\n", $_->getName(), $v->getData());
		}
		elsif ($type == ResultType->FILE)
		{
			my $v = $_->getValue();
			print sprintf("\t-> file result type %s = %s\n", $_->getName(), $v->getUrl());
			$v->saveTo("/tmp");
		}
	}
}
