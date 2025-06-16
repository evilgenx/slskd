import {
  filterResponse,
  getResponses,
  parseFiltersFromString,
} from '../../../lib/searches';
import { sleep } from '../../../lib/util';
import ErrorSegment from '../../Shared/ErrorSegment';
import LoaderSegment from '../../Shared/LoaderSegment';
import Switch from '../../Shared/Switch';
import Response from '../Response';
import SearchDetailHeader from './SearchDetailHeader';
import React, { useEffect, useMemo, useState } from 'react';
import { Button, Checkbox, Dropdown, Input, Segment } from 'semantic-ui-react';

const sortDropdownOptions = [
  {
    key: 'uploadSpeed',
    text: 'Upload Speed (Fastest to Slowest)',
    value: 'uploadSpeed',
  },
  {
    key: 'queueLength',
    text: 'Queue Depth (Least to Most)',
    value: 'queueLength',
  },
  {
    key: 'fileSize',
    text: 'File Size (Largest to Smallest)',
    value: 'fileSize',
  },
  {
    key: 'fileName',
    text: 'File Name (A-Z)',
    value: 'fileName',
  },
];

const SearchDetail = ({
  creating,
  disabled,
  onCreate,
  onRemove,
  onStop,
  removing,
  search,
  stopping,
}) => {
  const { fileCount, id, isComplete, lockedFileCount, responseCount, state } =
    search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);

  // filters and sorting options
  const [hiddenResults, setHiddenResults] = useState([]);
  const [resultSort, setResultSort] = useState('uploadSpeed');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [hideDuplicates, setHideDuplicates] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState('');
  const [loadedCount, setLoadedCount] = useState(0);
  const PAGE_SIZE = 25; // Define a page size for lazy loading

  // when the search transitions from !isComplete -> isComplete,
  // fetch the initial results from the server
  useEffect(() => {
    const getInitialResponses = async () => {
      try {
        setLoading(true);
        // the results may not be ready yet.  this is very rare, but
        // if it happens the search will complete with no results.
        await sleep(500);

        const initialResponses = await getResponses({ id, skip: 0, take: PAGE_SIZE });
        setResults(initialResponses);
        setLoadedCount(initialResponses.length);
        setLoading(false);
      } catch (getError) {
        setError(getError);
        setLoading(false);
      }
    };

    if (isComplete) {
      getInitialResponses();
    }
  }, [id, isComplete]);

  const loadMoreResponses = async () => {
    try {
      setLoading(true);
      const newResponses = await getResponses({ id, skip: loadedCount, take: PAGE_SIZE });
      setResults((prevResults) => [...prevResults, ...newResponses]);
      setLoadedCount((prevCount) => prevCount + newResponses.length);
      setLoading(false);
    } catch (getError) {
      setError(getError);
      setLoading(false);
    }
  };

  // apply sorting and filters.  this can take a while for larger result
  // sets, so memoize it.
  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      queueLength: { field: 'queueLength', order: 'asc' },
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
    };

    const filters = parseFiltersFromString(resultFilters);

    let processedResults = results
      .filter((r) => !hiddenResults.includes(r.username))
      .map((r) => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] };
        }

        return r;
      })
      .map((response) => filterResponse({ filters, response }))
      .filter((r) => r.fileCount + r.lockedFileCount > 0)
      .filter((r) => !(hideNoFreeSlots && !r.hasFreeUploadSlot));

    if (hideDuplicates) {
      const seenFiles = new Set();
      const uniqueResults = [];

      for (const response of processedResults) {
        const uniqueFilesForResponse = [];
        for (const file of response.files) {
          const fileIdentifier = `${file.filename}-${file.size}`;
          if (!seenFiles.has(fileIdentifier)) {
            seenFiles.add(fileIdentifier);
            uniqueFilesForResponse.push(file);
          }
        }
        if (uniqueFilesForResponse.length > 0) {
          uniqueResults.push({ ...response, files: uniqueFilesForResponse });
        }
      }
      processedResults = uniqueResults;
    }

    return processedResults.sort((a, b) => {
      if (resultSort === 'fileSize') {
        const aSize = a.files.reduce((sum, file) => sum + file.size, 0);
        const bSize = b.files.reduce((sum, file) => sum + file.size, 0);
        return bSize - aSize; // Largest to Smallest
      } else if (resultSort === 'fileName') {
        const aFileName = a.files[0]?.filename || '';
        const bFileName = b.files[0]?.filename || '';
        return aFileName.localeCompare(bFileName); // A-Z
      } else {
        const { field, order } = sortOptions[resultSort];
        if (order === 'asc') {
          return a[field] - b[field];
        }
        return b[field] - a[field];
      }
    });
  }, [
    hiddenResults,
    hideDuplicates,
    hideLocked,
    hideNoFreeSlots,
    resultFilters,
    results,
    resultSort,
  ]);

  // when a user uses the action buttons, we will *probably* re-use this component,
  // but with a new search ID.  clear everything to prepare for the transition

  const reset = () => {
    setLoading(false);
    setError(undefined);
    setResults([]);
    setHiddenResults([]);
    setLoadedCount(0);
  };

  const create = async ({ navigate, search: searchForCreate }) => {
    reset();
    onCreate({ navigate, searchForCreate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
  };

const filteredCount = results?.length - sortedAndFilteredResults.length;
const loaded = !removing && !creating && !loading && results;

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  return (
    <>
      <SearchDetailHeader
        creating={creating}
        disabled={disabled}
        loaded={loaded}
        loading={loading}
        onCreate={create}
        onRemove={remove}
        onStop={onStop}
        removing={removing}
        search={search}
        stopping={stopping}
      />
      <Switch
        loading={loading && <LoaderSegment />}
        searching={
          !isComplete && (
            <LoaderSegment>
              {state === 'InProgress'
                ? `Found ${fileCount} files ${
                    lockedFileCount > 0
                      ? `(plus ${lockedFileCount} locked) `
                      : ''
                  }from ${responseCount} users`
                : 'Loading results...'}
            </LoaderSegment>
          )
        }
      >
        {loaded && (
          <Segment
            className="search-options"
            raised
          >
            <Dropdown
              button
              className="search-options-sort icon"
              floating
              icon="sort"
              labeled
              onChange={(_event, { value }) => setResultSort(value)}
              options={sortDropdownOptions}
              text={
                sortDropdownOptions.find((o) => o.value === resultSort).text
              }
            />
            <div className="search-option-toggles">
              <Checkbox
                checked={hideLocked}
                className="search-options-hide-locked"
                label="Hide Locked Results"
                onChange={() => setHideLocked(!hideLocked)}
                toggle
              />
              <Checkbox
                checked={hideNoFreeSlots}
                className="search-options-hide-no-slots"
                label="Hide Results with No Free Slots"
                onChange={() => setHideNoFreeSlots(!hideNoFreeSlots)}
                toggle
              />
              <Checkbox
                checked={hideDuplicates}
                className="search-options-hide-duplicates"
                label="Hide Duplicates"
                onChange={() => setHideDuplicates(!hideDuplicates)}
                toggle
              />
              <Checkbox
                checked={foldResults}
                className="search-options-fold-results"
                label="Fold Results"
                onChange={() => setFoldResults(!foldResults)}
                toggle
              />
            </div>
            <Input
              action={
                Boolean(resultFilters) && {
                  color: 'red',
                  icon: 'x',
                  onClick: () => setResultFilters(''),
                }
              }
              className="search-filter"
              label={{ content: 'Filter', icon: 'filter' }}
              onChange={(_event, data) => setResultFilters(data.value)}
              placeholder="lackluster container -bothersome iscbr|isvbr islossless|islossy minbitrate:320 minbitdepth:24 minfilesize:10 minfilesinfolder:8 minlength:5000"
              value={resultFilters}
            />
          </Segment>
        )}
        {loaded &&
          sortedAndFilteredResults.map((r) => (
            <Response
              disabled={disabled}
              isInitiallyFolded={foldResults}
              key={r.username}
              onHide={() => setHiddenResults([...hiddenResults, r.username])}
              response={r}
            />
          ))}
        {loaded &&
          (loadedCount < responseCount ? ( // Assuming responseCount from search object is the total count
            <Button
              className="showmore-button"
              fluid
              onClick={loadMoreResponses}
              primary
              size="large"
            >
              Show More Results ({responseCount - loadedCount} remaining)
            </Button>
          ) : filteredCount > 0 ? (
            <Button
              className="showmore-button"
              disabled
              fluid
              size="large"
            >{`All results shown. ${filteredCount} results hidden by filter(s)`}</Button>
          ) : (
            ''
          ))}
      </Switch>
    </>
  );
};

export default SearchDetail;
