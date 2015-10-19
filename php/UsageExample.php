<?php
	
	require_once("AppChains.php");
	
	$chains = new AppChains("147fc0683b08e94c6c2835efba60b815eb501a13", "da.sequencing.com");
	
	echo "Beacons\n";
	$beaconResult = $chains->getPublicBeacon(1, 2, "A");
	print $beaconResult;

	echo "Chains\n\n";
	$chainsRawResult = $chains->getRawReport("StartApp", "MelanomaDsApp", "FILE:80599");
	print_r($chainsRawResult);
		
	$chainsResult = $chains->getReport("StartApp", "MelanomaDsApp", "FILE:80599");
	
	if ($chainsResult->isSucceeded())
		echo "Request has succeeded\n";
	else
		echo "Request has failed\n";
	
	foreach ($chainsResult->getResults() as $r)
	{
		$type = $r->getValue()->getType();
		
		switch ($type)
		{
			case ResultType::TEXT:
				$v = $r->getValue();
				echo sprintf("-> text result type %s = %s\n", $r->getName(), $v->getData());
			break;
			case ResultType::FILE:
				$v = $r->getValue();
				echo sprintf(" -> file result type %s = %s\n", $r->getName(), $v->getUrl());
				$v->saveTo("/tmp");
			break;
		}
	}
	
	
?>