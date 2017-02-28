<?php
	
	require_once("AppChains.php");

	function printReport($chainsResult)
    {
        if ($chainsResult->isSucceeded())
            echo "Request has succeeded\n";
        else
            echo "Request has failed\n";

        foreach ($chainsResult->getResults() as $r) {
            $type = $r->getValue()->getType();

            switch ($type) {
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
    }

    $chains = new AppChains("<your token goes here>", "api.sequencing.com");
	
	echo "Beacons\n";
	/*$beaconResult = $chains->getPublicBeacon(1, 2, "A");
	print $beaconResult;*/

	echo "Chains\n\n";
	$chainsRawResult = $chains->getRawReport("StartApp", "Chain9", "227680");
	print_r($chainsRawResult);
		
	$chainsResult = $chains->getReport("StartApp", "Chain90", "227680");

    /**
     * @param $chainsResult
     */
    printReport($chainsResult);

    $chainsBatchResult = $chains->getBatchReport("StartAppBatch", array("Chain91" => "227680", "Chain88" => "227680"));

    foreach ($chainsBatchResult as $key => $value){
        echo "-> Chain ID:";
        echo $key;
        echo "\n";
        printReport($value);
    }


	
?>