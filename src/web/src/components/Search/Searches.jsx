import './Search.css';
import React, { useEffect, useRef, useState } from 'react';
import { useHistory, useParams, useRouteMatch } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Card, Icon, Input, List, Segment } from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';
import { createSearchHubConnection } from '../../lib/hubFactory';
import * as library from '../../lib/searches';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import SearchDetail from './Detail/SearchDetail';
import SearchList from './List/SearchList';

const SEARCH_HISTORY_KEY = 'slskd_search_history';
const MAX_SEARCH_HISTORY = 10;

const Searches = ({ server } = {}) => {
  const [connecting, setConnecting] = useState(true);
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});

  const [removing, setRemoving] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [creating, setCreating] = useState(false);
  const [searchHistory, setSearchHistory] = useState([]);

  const inputRef = useRef();

  const { id: searchId } = useParams();
  const history = useHistory();
  const match = useRouteMatch();

  const onConnecting = () => {
    setConnecting(true);
  };

  const onConnected = () => {
    setConnecting(false);
    setError(undefined);
  };

  const onConnectionError = (connectionError) => {
    setConnecting(false);
    setError(connectionError);
  };

  const onUpdate = (update) => {
    setSearches(update);
    onConnected();
  };

  useEffect(() => {
    onConnecting();

    // Load search history from local storage
    try {
      const storedHistory = JSON.parse(
        localStorage.getItem(SEARCH_HISTORY_KEY),
      );
      if (Array.isArray(storedHistory)) {
        setSearchHistory(storedHistory);
      }
    } catch (error_) {
      console.error(
        'Failed to parse search history from local storage',
        error_,
      );
      localStorage.removeItem(SEARCH_HISTORY_KEY); // Clear corrupted data
    }

    const searchHub = createSearchHubConnection();

    searchHub.on('list', (searchesEvent) => {
      onUpdate(
        searchesEvent.reduce((accumulator, search) => {
          accumulator[search.id] = search;
          return accumulator;
        }, {}),
      );
      onConnected();
    });

    searchHub.on('update', (search) => {
      onUpdate((old) => ({ ...old, [search.id]: search }));
    });

    searchHub.on('delete', (search) => {
      onUpdate((old) => {
        delete old[search.id];
        return { ...old };
      });
    });

    searchHub.on('create', (search) => {
      onUpdate((old) => ({ ...old, [search.id]: search }));
    });

    searchHub.onreconnecting((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );
    searchHub.onreconnected(() => onConnected());
    searchHub.onclose((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );

    const connect = async () => {
      try {
        onConnecting();
        await searchHub.start();
      } catch (connectionError) {
        toast.error(connectionError?.message ?? 'Failed to connect');
        onConnectionError(connectionError?.message ?? 'Failed to connect');
      }
    };

    connect();

    return () => {
      searchHub.stop();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // create a new search, and optionally navigate to it to display the details
  // we do this if the user clicks the search icon, or repeats an existing search
  const create = async ({ navigate = false, search } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = search || ref.value;
    const id = uuidv4();

    try {
      setCreating(true);
      await library.create({ id, searchText });

      try {
        ref.value = '';
        ref.focus();
      } catch {
        // we are probably repeating an existing search; the input isn't mounted.  no-op.
      }

      setCreating(false);

      // Add to search history
      setSearchHistory((previousHistory) => {
        const newHistory = [
          searchText,
          ...previousHistory.filter((item) => item !== searchText),
        ];
        const trimmedHistory = newHistory.slice(0, MAX_SEARCH_HISTORY);
        localStorage.setItem(
          SEARCH_HISTORY_KEY,
          JSON.stringify(trimmedHistory),
        );
        return trimmedHistory;
      });

      if (navigate) {
        history.push(`${match.url.replace(`/${searchId}`, '')}/${id}`);
      }
    } catch (createError) {
      console.error(createError);
      toast.error(
        createError?.response?.data ?? createError?.message ?? createError,
      );
      setCreating(false);
    }
  };

  const reSearch = (text) => {
    inputRef.current.inputRef.current.value = text;
    create();
  };

  // delete a search
  const remove = async (search) => {
    try {
      setRemoving(true);

      await library.remove({ id: search.id });
      setSearches((old) => {
        delete old[search.id];
        return { ...old };
      });

      setRemoving(false);
    } catch (error_) {
      console.error(error_);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRemoving(false);
    }
  };

  // stop an in-progress search
  const stop = async (search) => {
    try {
      setStopping(true);
      await library.stop({ id: search.id });
      setStopping(false);
    } catch (stoppingError) {
      console.error(stoppingError);
      toast.error(
        stoppingError?.response?.data ??
          stoppingError?.message ??
          stoppingError,
      );
      setStopping(false);
    }
  };

  if (connecting) {
    return <LoaderSegment />;
  }

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  // if searchId is not null, there's an id in the route.
  // display the details for the search, if there is one
  if (searchId) {
    if (searches[searchId]) {
      return (
        <SearchDetail
          creating={creating}
          disabled={!server?.isConnected}
          onCreate={create}
          onRemove={remove}
          onStop={stop}
          removing={removing}
          search={searches[searchId]}
          stopping={stopping}
        />
      );
    }

    // if the searchId doesn't match a search we know about, chop
    // the id off of the url and force navigation back to the list
    history.replace(match.url.replace(`/${searchId}`, ''));
  }

  inputRef?.current?.inputRef?.current.focus();

  return (
    <>
      <Segment
        className="search-segment"
        raised
      >
        <div className="search-segment-icon">
          <Icon
            name="search"
            size="big"
          />
        </div>
        <Input
          action={
            <>
              <Button
                disabled={creating || !server.isConnected}
                icon="plus"
                onClick={create}
              />
              <Button
                disabled={creating || !server.isConnected}
                icon="search"
                onClick={() => create({ navigate: true })}
              />
            </>
          }
          className="search-input"
          disabled={creating || !server.isConnected}
          input={
            <input
              data-lpignore="true"
              placeholder={
                server.isConnected
                  ? 'Search phrase (e.g. "artist .mp3")'
                  : 'Connect to server to perform a search'
              }
              type="search"
            />
          }
          loading={creating}
          onKeyUp={(keyUpEvent) => (keyUpEvent.key === 'Enter' ? create() : '')}
          placeholder="Search phrase (e.g. 'artist .mp3')"
          ref={inputRef}
          size="big"
        />
        <div className="file-type-filters">
          <Button.Group>
            <Button
              basic
              onClick={() => {
                inputRef.current.inputRef.current.value += ' .mp3';
              }}
            >
              MP3
            </Button>
            <Button
              basic
              onClick={() => {
                inputRef.current.inputRef.current.value += ' .flac';
              }}
            >
              FLAC
            </Button>
            <Button
              basic
              onClick={() => {
                inputRef.current.inputRef.current.value += ' .mp4';
              }}
            >
              MP4
            </Button>
            <Button
              basic
              onClick={() => {
                inputRef.current.inputRef.current.value += ' .mkv';
              }}
            >
              MKV
            </Button>
            <Button
              basic
              onClick={() => {
                inputRef.current.inputRef.current.value += ' .pdf';
              }}
            >
              PDF
            </Button>
          </Button.Group>
        </div>
      </Segment>
      {searchHistory.length > 0 && (
        <Segment raised>
          <Card.Header>Recent Searches</Card.Header>
          <List
            divided
            relaxed
          >
            {searchHistory.map((searchText) => (
              <List.Item key={searchText}>
                <List.Content floated="right">
                  <Button
                    onClick={() => reSearch(searchText)}
                    size="tiny"
                  >
                    Re-search
                  </Button>
                </List.Content>
                <List.Content>{searchText}</List.Content>
              </List.Item>
            ))}
          </List>
        </Segment>
      )}
      {Object.keys(searches).length === 0 ? (
        <PlaceholderSegment
          caption="No searches to display"
          icon="search"
        />
      ) : (
        <SearchList
          connecting={connecting}
          error={error}
          onRemove={remove}
          onStop={stop}
          searches={searches}
        />
      )}
    </>
  );
};

export default Searches;
