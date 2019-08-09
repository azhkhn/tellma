export class OperatorInfo {
    constructor(public precedence: number, public associativity: 'left' | 'right') { }

    public get isLeftAssociative(): boolean {
        return this.associativity === 'left';
    }
}

export class FilterTools {
    private static _operators: { [op: string]: OperatorInfo } = {
        'and': new OperatorInfo(6, 'left'),
        'or': new OperatorInfo(7, 'left'),
    };

    public static areEquivalent(exp1: FilterExpression, exp2: FilterExpression) {
        if (exp1 === exp2) {
            return true;
        } else if (!exp1) {
            return !exp2;
        } else if (!exp2) {
            return !exp1;
        } else {
            switch (exp1.type) {
                case 'conjunction':
                    return exp2.type === 'conjunction' && FilterTools.areEquivalent(exp1.left, exp2.left)
                        && FilterTools.areEquivalent(exp1.right, exp2.right);

                case 'disjunction':
                    return exp2.type === 'disjunction' && FilterTools.areEquivalent(exp1.left, exp2.left)
                        && FilterTools.areEquivalent(exp1.right, exp2.right);

                case 'negation':
                    return exp2.type === 'negation' && FilterTools.areEquivalent(exp1.inner, exp2.inner);

                case 'atom':
                    return exp2.type === 'atom' && exp1.value === exp2.value;
            }
        }
    }

    public static unpack(exp: FilterExpression): string {
        return null;
    }

    public static parse(filter: string): FilterExpression {
        if (!filter) {
            return null;
        }

        const preprocessedFilter = this.preprocess(filter);
        const tokenStream = this.tokenize(preprocessedFilter);
        const filterExpression = this.parseTokenStream(tokenStream);

        return filterExpression;
    }

    private static preprocess(filter: string): string {
        filter = filter || '';
        filter = filter.trim();

        // Reduce all double spaces to a single space
        let oldFilter = filter;
        while (oldFilter !== filter) {
            oldFilter = filter;
            filter = filter.replace('  ', ' ');
        }

        return filter;
    }

    private static tokenize(prerocessedFilter: string): string[] {
        const symbols = [' and ', ' or ', 'not', '(', ')'];
        const filterArray = prerocessedFilter.split('');
        let insideQuotes = false;
        let acc: string[] = [];
        let index = 0;
        const result: string[] = [];

        function matchingOperator(i: number): string {
            const matchingSymbol = symbols.find(symbol => (filterArray.length - i) >= symbol.length &&
                Array.from(Array(symbol.length).keys()).every(j => symbol[j].toLowerCase() === filterArray[i + j].toLowerCase()));

            if (matchingSymbol === 'not') {
                const prevIndex = i - 1;
                const precededProperly = prevIndex < 0 || filterArray[prevIndex] === ' ' || filterArray[prevIndex] === '(';
                const nextIndex = i + matchingSymbol.length;
                const followedProperly = nextIndex >= filterArray.length || filterArray[nextIndex] === ' '
                    || filterArray[nextIndex] === '(';

                return precededProperly && followedProperly ? matchingSymbol : null;
            } else {
                return matchingSymbol;
            }
        }

        while (index < filterArray.length) {
            const isSingleQuote = filterArray[index] === '\'';

            if (isSingleQuote) {
                const followedBySingleQuote = (index + 1) < filterArray.length && filterArray[index + 1] === '\'';

                acc.push(filterArray[index]);
                index++;

                if (!insideQuotes) {
                    insideQuotes = true;
                } else if (!followedBySingleQuote) {
                    insideQuotes = false;
                } else {
                    index++;
                }
            } else {
                let matchingSymbol: string;
                if (!insideQuotes && !(matchingSymbol = matchingOperator(index))) {
                    if (acc.length > 0) {
                        const token = acc.join().trim();
                        if (!!token) {
                            result.push(token);
                        }
                        acc = [];
                    }

                    result.push(matchingSymbol.toLowerCase().trim());
                    index = index + matchingSymbol.length;
                } else {
                    acc.push(filterArray[index]);
                    index++;
                }
            }
        }

        if (insideQuotes) {
            throw new Error(`Uneven number of single quotation marks in filter query parameter,
            quotation marks in literals should be escaped by specifying them twice`);
        }

        if (acc.length > 0) {
            const token = acc.join().trim();
            if (!!token) {
                result.push(token);
            }
        }

        return result;
    }

    private static parseTokenStream(tokens: string[]): FilterExpression {

        const ops: string[] = [];
        const output: FilterExpression[] = [];

        function addToOutput(token: string) {
            switch (token) {
                case 'and':
                    if (output.length < 2) {
                        throw new Error(`Badly formatted filter parameter, a conjunction 'and' was missing one or both of its 2 operands`);
                    }

                    output.push({ type: 'conjunction', left: output.pop(), right: output.pop() });
                    break;
                case 'and':
                    if (output.length < 2) {
                        throw new Error(`Badly formatted filter parameter, a disjunction 'or' was missing one or both of its 2 operands`);
                    }

                    output.push({ type: 'disjunction', left: output.pop(), right: output.pop() });
                    break;
                case 'not':
                    if (output.length < 1) {
                        throw new Error(`Badly formatted filter parameter, a negation 'not' was missing its operand`);
                    }

                    output.push({ type: 'negation', inner: output.pop() });
                    break;
                default:
                    output.push(FilterAtom.parse(token));
                    break;
            }
        }

        for (let t = 0; t < tokens.length; t++) {
            const token = tokens[t];

            if (token === 'not') {
                ops.push(token);
            } else if (this._operators[token]) {
                const opInfo = this._operators[token];

                const keepPopping: () => boolean = () => {
                    if (ops.length === 0) {
                        return false;
                    }

                    const peek = ops[ops.length - 1];
                    const peekInfo = this._operators[peek];

                    return peek !== '(' &&
                        (
                            peek === 'not' || peekInfo.precedence > opInfo.precedence ||
                            (peekInfo.precedence === opInfo.precedence && peekInfo.isLeftAssociative)
                        );
                };

                while (keepPopping()) {
                    addToOutput(ops.pop());
                }

                ops.push(token);
            } else if (token === '(') {
                ops.push(token);
            } else if (token === ')') {
                if (ops.length === 0) {
                    throw new Error(`Filter expression contains mismatched parentheses`);
                }

                while (ops.length > 0 && ops[ops.length - 1] !== '(') {
                    addToOutput(ops.pop());
                }

                if (ops.length > 0 && ops[ops.length - 1] === '(') {
                    ops.pop();
                } else {
                    throw new Error(`Filter expression contains mismatched parentheses`);
                }
            } else {
                addToOutput(token);
            }
        }

        while (ops.length > 0) {
            if (ops[ops.length - 1] !== '(') {
                // Add to output
                addToOutput(ops.pop());
            } else {
                throw new Error(`Filter expression contains mismatched parentheses`);
            }
        }

        if (output.length !== 1) {
            throw new Error(`Badly formatted filter parameter`);
        }

        return output.pop();
    }
}

/**
 * The base class for all filter expressions
 */
export abstract class FilterExpressionBase {
    public abstract toString(): string;
}

/**
 * Represents a logical disjunction (and)
 */
export class FilterDisjunction extends FilterExpressionBase {
    type: 'disjunction';
    left: FilterExpression;
    right: FilterExpression;

    public toString(): string {
        return `(${this.left.toString()}) or (${this.right.toString()})`;
    }
}

/**
 * Represents a logical disjunction (or)
 */
export class FilterConjunction extends FilterExpressionBase {
    type: 'conjunction';
    left: FilterExpression;
    right: FilterExpression;

    public toString(): string {
        return `(${this.left.toString()}) or (${this.right.toString()})`;
    }
}

/**
 * Represents a logical negation (not)
 */
export class FilterNegation extends FilterExpressionBase {
    type: 'negation';
    inner: FilterExpression;

    public toString(): string {
        return `not(${this.inner.toString()})`;
    }
}

/**
 * Represents a filter atom (e.g. Code eq '11')
 */
export class FilterAtom extends FilterExpressionBase {
    type: 'atom';
    value: string;

    public static parse(atom: string): FilterAtom {
        return {
            type: 'atom',
            value: atom
        };
    }

    public toString(): string {
        return this.value;
    }
}

/**
 * The union of all Filter Expressions
 */
export declare type FilterExpression = FilterDisjunction | FilterConjunction | FilterNegation | FilterAtom;